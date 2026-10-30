# Estudo do projeto: StockCore API

Guia para entender e explicar o projeto. Leia com o código aberto ao lado: cada seção aponta os arquivos.

---

## 1. Arquitetura em uma frase

Uma API REST em **ASP.NET Core** que **autentica** com JWT, **valida** a entrada com FluentValidation, aplica as **regras de estoque** no domínio e grava no **PostgreSQL** com **Entity Framework Core**, sempre dentro de uma transação quando o saldo muda.

### Os projetos e o papel de cada um

| Projeto                    | Exemplo                                       | Responsabilidade                                                     | Não deve                       |
| -------------------------- | --------------------------------------------- | -------------------------------------------------------------------- | ------------------------------ |
| `StockCore.Domain`         | `Entities/Product.cs`, `Rules/StockRules.cs`  | entidades e regras puras: "pode sair?", "pode cancelar?"             | conhecer EF Core, HTTP ou JSON |
| `StockCore.Application`    | `Movements/MovementDtos.cs`, validadores      | contratos dos casos de uso (interfaces), DTOs e validação da entrada | acessar o banco                |
| `StockCore.Infrastructure` | `Services/MovementService.cs`, `Persistence/` | implementar os casos de uso com EF Core, transações, JWT e hash      | decidir status HTTP            |
| `StockCore.Api`            | `Controllers/`, `Errors/`, `Program.cs`       | rotas, autenticação, autorização por papel, erros e Swagger          | ter regra de negócio           |

As dependências apontam para dentro: `Api → Application → Domain` e `Infrastructure → Application → Domain`. Só a `Api` conhece todos, porque é ela que monta a injeção de dependência em `Program.cs`.

Por que separar assim? Cada parte muda por um motivo diferente. Se a regra de cancelamento mudar, mexo em `StockRules`. Se a tabela mudar, mexo na configuração do EF e gero uma migration. E as regras do domínio são testadas sem banco nenhum (`tests/StockCore.UnitTests`).

Por que **não** usei MediatR, CQRS ou repositório genérico? Para o tamanho do projeto, eles só adicionariam camadas. Um serviço por área (`ProductService`, `MovementService`...) usando o `DbContext` direto é mais fácil de ler e de explicar. O `DbContext` já é uma unidade de trabalho e cada `DbSet` já funciona como um repositório.

---

## 2. Fluxo de uma requisição (saída de estoque)

`POST /api/movements/exits` com `{ "productId": "...", "quantity": 3 }`:

1. **Autenticação** (`AuthenticationSetup.cs`): o `JwtBearer` lê `Authorization: Bearer <token>`, confere assinatura, emissor, público e validade. Sem token ou token inválido: o evento `OnChallenge` responde **401** em ProblemDetails.
2. **Autorização**: `[Authorize(Roles = "Admin,Operator")]` no `MovementsController`. Um `Viewer` recebe **403** (evento `OnForbidden`).
3. **Leitura do corpo**: o JSON vira um `ExitRequest`. Se o JSON estiver quebrado ou `quantity` vier como `1.5` ou `"dez"`, o `InvalidModelStateResponse` responde **400**.
4. **Validação** (`ValidationFilter`): o filtro procura um `IValidator<ExitRequest>` e roda o `ExitRequestValidator` (quantidade entre 1 e 1.000.000, produto informado). Erro: **400** com `errors: { "quantity": [...] }`.
5. **Controller**: pega o id do usuário na claim `sub` e chama `MovementService.RegisterExitAsync`.
6. **Serviço**, dentro de uma transação:
    - `SELECT 1 FROM products WHERE id = @id FOR UPDATE`: trava a linha do produto até o fim da transação;
    - carrega o produto (já com o saldo mais recente);
    - `product.RegisterExit(3, ...)` chama `StockRules.EnsureCanRemove`: produto inativo ou saldo insuficiente lançam `BusinessRuleException`;
    - adiciona o `StockMovement` e chama `SaveChanges` (INSERT do movimento + UPDATE do saldo);
    - `COMMIT`.
7. **Resposta 201** com o movimento, o saldo atual, o mínimo e `lowStockAlert`.
8. Se qualquer exceção aparecer no caminho, o **`GlobalExceptionHandler`** converte: `BusinessRuleException` vira 422, `NotFoundException` 404, `ConflictException` 409, e erros do banco (unique violation, check violation) também viram 409/422.

---

## 3. Entity Framework Core

### DbContext

`AppDbContext` tem um `DbSet` por entidade e aplica as configurações de `Persistence/Configurations` com `ApplyConfigurationsFromAssembly`. Cada configuração (`IEntityTypeConfiguration<T>`) diz tamanho das colunas, índices, chaves estrangeiras e check constraints. Assim as entidades do domínio ficam limpas, sem atributos do EF.

O nome das tabelas e colunas sai em `snake_case` (`current_stock`, `stock_movements`) pelo pacote `EFCore.NamingConventions`, que é o padrão mais comum no PostgreSQL.

### Migrations

-   `dotnet ef migrations add Nome` compara o modelo atual com o snapshot da última migration e gera o código C# com as diferenças (`Persistence/Migrations`).
-   `dotnet ef database update` (ou `Database.MigrateAsync()` ao iniciar, se `Database:ApplyMigrationsOnStartup=true`) aplica as que faltam e registra cada uma na tabela `__EFMigrationsHistory`.
-   `DesignTimeDbContextFactory` existe para o `dotnet-ef` criar o contexto sem subir a API inteira.
-   O CI roda `dotnet ef migrations has-pending-model-changes`: se alguém mudar uma entidade e esquecer a migration, o pipeline falha.
-   Os testes de integração sobem a API com `ApplyMigrationsOnStartup=true`, então toda execução do CI também prova que as migrations criam o banco do zero.

### Tracking

-   Consultas de leitura usam `AsNoTracking()` e projeção com `Select` direto para o DTO: o EF busca só as colunas necessárias e não guarda os objetos na memória.
-   Quando vou alterar (ex.: `product.RegisterExit`), carrego **com** tracking: o EF lembra o estado original e, no `SaveChanges`, gera só o `UPDATE` das colunas que mudaram.

### Transações

`MovementService.ExecuteAsync` abre `BeginTransactionAsync`, trava o produto, aplica a regra, grava e faz `CommitAsync`. Se a regra lançar exceção, o `await using` descarta a transação sem commit, então nada é gravado (rollback). O cancelamento grava três coisas na mesma transação: o original marcado como cancelado, o estorno e o novo saldo.

### Concorrência

Dois mecanismos, para dois problemas diferentes:

| Problema                                                   | Solução                                   | Onde                                    |
| ---------------------------------------------------------- | ----------------------------------------- | --------------------------------------- |
| Duas saídas ao mesmo tempo lendo o mesmo saldo             | trava pessimista: `SELECT ... FOR UPDATE` | `MovementService.LockProductAsync`      |
| Duas pessoas editando o cadastro do produto ao mesmo tempo | trava otimista: `xmin` como token         | `ProductConfiguration` (`IsRowVersion`) |

Com o `FOR UPDATE`, a segunda transação espera a primeira terminar e então lê o saldo já atualizado. O teste `Saidas_simultaneas_nunca_deixam_o_saldo_negativo` dispara 10 saídas de 1 unidade em paralelo com saldo 5: exatamente 5 passam e 5 recebem 422. Mesmo que essa trava fosse removida por engano, a check constraint `current_stock >= 0` ainda impediria saldo negativo no banco.

Com o `xmin`, o EF inclui `WHERE xmin = @versaoLida` no `UPDATE`. Se outra transação mudou a linha antes, nenhuma linha é afetada, o EF lança `DbUpdateConcurrencyException` e a API responde 409.

---

## 4. Autenticação e autorização

-   **Senha**: `PasswordHasher<User>` do ASP.NET Core Identity (PBKDF2 com salt aleatório e muitas iterações). Uso só o hasher, não o Identity inteiro, para não trazer tabelas e telas que o projeto não precisa.
-   **Login** (`AuthService`): normaliza o e-mail, compara o hash e gera o token. E-mail inexistente e senha errada dão a **mesma mensagem** (401). No caso do e-mail inexistente, o código verifica um hash qualquer mesmo assim, para o tempo de resposta não denunciar quais e-mails existem.
-   **JWT** (`JwtTokenService`): HS256, com `sub` (id), `email`, `name`, `role` e `jti`, emissor, público e validade configuráveis. A chave vem de `Jwt:Key` (variável de ambiente ou user-secrets) e precisa ter 32+ caracteres; sem ela a aplicação não sobe (`ValidateOnStart`).
-   **Autorização por papel**: o `RoleClaimType` aponta para a claim `role`, então `[Authorize(Roles = "Admin")]` funciona direto. `Viewer` só lê; `Operator` registra entradas e saídas; `Admin` cancela, cadastra e cria usuários.
-   **401 x 403**: 401 é "não sei quem você é" (sem token, token inválido); 403 é "sei quem você é, mas seu papel não permite".

---

## 5. Regras de negócio (e onde estão)

| Regra                                                    | Onde                                                            | Teste                                                                                  |
| -------------------------------------------------------- | --------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Saldo nunca negativo                                     | `StockRules.EnsureCanRemove` + check constraint                 | `StockRulesTests`, `ConcurrencyTests`                                                  |
| Saída maior que o saldo: 422 com `available`/`requested` | `StockRules.EnsureCanRemove`                                    | `Saida_maior_que_o_saldo_devolve_422_com_disponivel_e_solicitado`                      |
| Sem DELETE de movimentação                               | não existe a rota                                               | `Movimentacoes_nao_podem_ser_apagadas` (405)                                           |
| Cancelamento com estorno, numa transação                 | `Product.CancelMovement`, `MovementService.CancelAsync`         | `Entrada_saida_e_cancelamento_mantem_saldo_e_historico`                                |
| Cancelar entrada exige saldo                             | `StockRules.EnsureCanCancel`                                    | `Cancelar_entrada_ja_consumida_devolve_422`                                            |
| Não cancela duas vezes nem cancela estorno               | `StockRules.EnsureCanCancel` + índice único em `reversal_of_id` | `Nao_cancela_duas_vezes_nem_cancela_um_estorno`, `Cancelamentos_simultaneos_...`       |
| Produto inativo não movimenta                            | `StockRules.EnsureCanReceive` / `EnsureCanRemove`               | `Produto_inativo_nao_aceita_entrada_nem_saida`                                         |
| Alerta de estoque mínimo (inclui o limite)               | `StockRules.IsLowStock`, `AlertService`                         | `Alerta_de_estoque_minimo_inclui_o_limite`, `Alerta_lista_so_produtos_ativos_...`      |
| Exclusões protegidas                                     | `CategoryService.DeleteAsync`, `ProductService.DeleteAsync`     | `Categoria_com_produtos_nao_e_excluida`, `Produto_com_movimentacao_nao_e_excluido_...` |
| Permissões por papel                                     | `[Authorize(Roles = ...)]`                                      | `Viewer_le_mas_nao_escreve`, `Cancelamento_exige_motivo_e_papel_admin`, ...            |
| SKU único, quantidade inteira > 0, preço ≥ 0             | `ProductService`, validadores                                   | `Sku_repetido_...`, `Quantidade_que_nao_e_inteiro_positivo_devolve_400`, ...           |

Decisão que vale explicar: **cancelar uma entrada inativa é permitido**, mesmo com o produto inativo. A regra "inativo não movimenta" vale para operações do dia a dia (entrada e saída); o cancelamento é uma correção do Admin, e bloquear isso obrigaria a reativar o produto só para corrigir um erro.

---

## 6. Erros padronizados

Todas as respostas de erro saem no formato **ProblemDetails** (RFC 9457), com dois campos a mais: `code` (estável, para o cliente tratar) e `traceId` (para achar a requisição no log).

```json
{
    "title": "Regra de negócio violada",
    "status": 422,
    "detail": "Estoque insuficiente para a saída.",
    "instance": "/api/movements/exits",
    "code": "BUSINESS_RULE_VIOLATION",
    "details": { "available": 3, "requested": 5 },
    "traceId": "0HN..."
}
```

Quatro lugares geram esse formato, todos pelo `ProblemFactory`:

1. `GlobalExceptionHandler` (`IExceptionHandler` do .NET 8): exceções da aplicação e do banco;
2. `ValidationFilter`: erros do FluentValidation;
3. `InvalidModelStateResponse`: JSON que nem chega a virar objeto;
4. eventos `OnChallenge`/`OnForbidden` do JwtBearer: 401 e 403.

O domínio não sabe nada de HTTP: ele só lança `BusinessRuleException`, `NotFoundException` etc. Quem decide o status é a API.

---

## 7. Testes

-   **Unitários** (`tests/StockCore.UnitTests`): `StockRules`, a entidade `Product` (entrada, saída, cancelamento, alerta), os validadores (com o `TestHelper` do FluentValidation) e os cálculos dos relatórios. Não usam banco.
-   **Integração** (`tests/StockCore.IntegrationTests`): o `ApiFactory` (um `WebApplicationFactory<Program>`) sobe a API inteira em memória apontando para um PostgreSQL real. Antes de cada teste as tabelas são limpas com `TRUNCATE` e são criados três usuários (Admin, Operator e Viewer) com tokens válidos. Os testes chamam as rotas por HTTP, como um cliente faria.
-   **Concorrência**: `Task.WhenAll` com várias requisições ao mesmo tempo no mesmo produto.
-   Os testes rodam em sequência (`DisableTestParallelization`) porque compartilham o mesmo banco.
-   **Teste de mutação manual**: quebrei de propósito a regra de saldo (`requested > available` virou `requested > available + 1000`) e rodei os testes: eles falharam, o que mostra que a regra está de fato coberta. Esse passo rodou num workflow do GitHub Actions e está no log da execução.

---

## 8. CI

`.github/workflows/ci.yml`, a cada push e pull request:

1. `dotnet restore` e `dotnet tool restore` (traz o `dotnet-ef` fixado em `.config/dotnet-tools.json`);
2. `dotnet format --verify-no-changes`;
3. `dotnet build -c Release` com `TreatWarningsAsErrors` (avisos do compilador e dos analisadores viram erro; avisos de auditoria do NuGet continuam como aviso, para uma vulnerabilidade nova num pacote não travar o build sem aviso prévio);
4. `dotnet ef migrations has-pending-model-changes`;
5. `dotnet test` com um container `postgres:16-alpine` como serviço;
6. na `main`: publica a API, sobe ela sem banco, baixa o `/swagger/v1/swagger.json` e publica no GitHub Pages junto com o `docs/index.html`.

As versões dos pacotes ficam todas em `Directory.Packages.props` (gerenciamento central), e o SDK é fixado em `global.json`.

---

## 9. Decisões técnicas

| Decisão                                   | Por quê                                                                                   | Alternativa considerada                         |
| ----------------------------------------- | ----------------------------------------------------------------------------------------- | ----------------------------------------------- |
| Controllers em vez de Minimal APIs        | organização por recurso, atributos de autorização e documentação mais conhecidos em vagas | Minimal APIs (menos código, mas mais espalhado) |
| `FOR UPDATE` nas movimentações            | simples, correto e fácil de testar; operações no mesmo produto são raras o bastante       | só token otimista com retry                     |
| Estorno em vez de DELETE                  | histórico auditável; saldo após cada movimento registrado                                 | apagar e recalcular                             |
| Produto nasce com saldo 0                 | todo saldo tem uma movimentação de origem                                                 | campo "estoque inicial" no cadastro             |
| 422 para regra de negócio                 | separa "dado inválido" (400) de "dado válido, mas não permitido agora" (422)              | 400 para tudo                                   |
| 409 para exclusão bloqueada e duplicidade | o pedido conflita com o estado atual do recurso                                           | 422                                             |
| FluentValidation                          | regras legíveis, mensagens em português e testáveis isoladamente                          | DataAnnotations                                 |
| `TimeProvider` injetado                   | a data atual vem de um serviço, o que facilita testar                                     | `DateTime.UtcNow` espalhado                     |
| Datas sempre em UTC (`timestamptz`)       | o Npgsql exige `DateTime` UTC e evita confusão de fuso                                    | horário local                                   |

---

## 10. Problemas reais encontrados

-   **Sem acesso ao NuGet no ambiente de desenvolvimento**: o ambiente onde o código foi escrito não conseguia baixar pacotes do NuGet. Só o projeto `Domain`, que não tem dependências, compilava ali. A solução foi usar o **GitHub Actions como ambiente de build**: `restore`, `build`, `format` e testes rodaram no runner, e a migration inicial foi gerada lá pelo próprio `dotnet-ef` (num workflow temporário que fez o commit da migration e depois foi removido). Por isso o primeiro commit da migration é do `github-actions[bot]`.
-   **Content-type dos erros de validação**: os erros do `ValidationFilter` saíam como `application/json` em vez de `application/problem+json`. Causa: o `[Produces("application/json")]` da classe base dos controllers sobrescreve o content-type de qualquer `ObjectResult`. Correção: devolver um `JsonResult`, que não passa por esse filtro. Os testes de integração pegaram o problema porque conferem o content-type de toda resposta de erro.
-   **SKU com espaços**: o validador recusa `" mer-001 "` pela expressão regular antes de a entidade normalizar. Mantive a regra (o cliente deve mandar o SKU limpo) e ajustei o teste.
-   **O banco também segurou a mutação**: no teste de mutação, com a regra do domínio quebrada, a saída maior que o saldo ainda foi recusada com 422, só que pela check constraint do banco (sem `details.available` e `details.requested`). O teste de integração falhou justamente por conferir esses detalhes. Foi uma confirmação prática de que a constraint funciona como segunda barreira.
-   **Acentos escapados**: as respostas de erro saíam com `"sa\u00EDda"` em vez de `"saída"`. O JSON era válido, mas ilegível em log. O MVC usa um encoder mais permissivo por padrão, e o `ProblemFactory` usava o padrão do `System.Text.Json`. Passei a usar o mesmo encoder nos dois e adicionei uma verificação no teste.

---

## 11. Perguntas de recrutador (com respostas)

**1. Me explica o projeto em 30 segundos.**
É uma API de controle de estoque em C# e .NET 8. Tem cadastro de produtos, categorias e fornecedores, entradas e saídas, cancelamento com estorno, alerta de estoque mínimo e relatórios. O foco foi nas regras: o saldo nunca fica negativo, nem com requisições simultâneas, e nada é apagado do histórico. Tem testes unitários e de integração com PostgreSQL real rodando no GitHub Actions.

**2. Por que separar em Domain, Application, Infrastructure e Api?**
Para cada parte mudar por um motivo só e para testar as regras sem banco. O Domain não depende de nada; a Application define o que o sistema faz; a Infrastructure implementa com EF Core; a Api só recebe HTTP e monta tudo. Não usei MediatR nem repositório genérico porque, nesse tamanho, só aumentariam o código.

**3. Como você garante que o estoque não fica negativo?**
Em três níveis. O domínio recusa a saída maior que o saldo e devolve 422 com o disponível e o pedido. A transação trava a linha do produto com `SELECT ... FOR UPDATE`, então duas saídas simultâneas não leem o mesmo saldo. E o banco tem uma check constraint `current_stock >= 0`, que é a última barreira.

**4. E se duas pessoas fizerem uma saída ao mesmo tempo?**
A primeira transação trava a linha; a segunda espera. Quando a primeira faz commit, a segunda lê o saldo já atualizado e a regra decide. Tenho um teste que dispara 10 saídas de 1 unidade em paralelo num produto com saldo 5: passam exatamente 5 e o saldo termina em 0.

**5. Qual a diferença entre concorrência otimista e pessimista? Onde você usou cada uma?**
Pessimista trava antes de ler (o `FOR UPDATE`): usei nas movimentações, onde o conflito é o problema principal. Otimista não trava; confere na hora de gravar se a linha mudou: usei a coluna `xmin` do PostgreSQL como token na edição do produto. Se mudou, o EF lança `DbUpdateConcurrencyException` e a API responde 409.

**6. Por que não existe DELETE de movimentação?**
Porque o histórico é a prova de como o saldo chegou ao valor atual. Um lançamento errado é cancelado: o original fica marcado como cancelado (com quem, quando e motivo) e um estorno desfaz o efeito. Tudo na mesma transação.

**7. O que acontece se tentarem cancelar uma entrada cujo estoque já saiu?**
A API recusa com 422: se eu desfizesse essa entrada, o saldo ficaria negativo. Também não deixo cancelar duas vezes nem cancelar um estorno, e há um índice único em `reversal_of_id` para o banco garantir um estorno por movimento.

**8. Como funciona a autenticação?**
Login com e-mail e senha; a senha é guardada com o `PasswordHasher` do ASP.NET Core Identity (PBKDF2 com salt). Se estiver certa, gero um JWT com o id, e-mail, nome e papel do usuário. Nas outras rotas o middleware JwtBearer valida assinatura, emissor, público e validade. A chave não fica no repositório: vem de variável de ambiente ou user-secrets.

**9. Como as permissões funcionam?**
Por papel, com `[Authorize(Roles = ...)]`. `Viewer` só consulta, `Operator` registra entradas e saídas, `Admin` cancela, cadastra e cria usuários. Sem token é 401; com token, mas sem permissão, é 403. Os testes cobrem os três papéis.

**10. Por que 400, 409 e 422 diferentes?**
400 é entrada malformada ou inválida (quantidade 0, JSON quebrado). 409 é conflito com o estado atual (SKU repetido, categoria com produtos). 422 é entrada válida, mas a regra de negócio não permite agora (saída maior que o saldo, produto inativo). Isso ajuda quem consome a API a tratar cada caso.

**11. Como você padronizou os erros?**
Com ProblemDetails, que é o padrão do ASP.NET Core, mais um campo `code` estável e o `traceId`. O domínio só lança exceções próprias; um `IExceptionHandler` converte cada tipo no status certo. Erros de validação, de leitura do JSON e de autenticação passam pela mesma fábrica, então o cliente sempre recebe o mesmo formato.

**12. Como você testou?**
Testes unitários das regras, da entidade Product, dos validadores e dos cálculos dos relatórios, sem banco. Testes de integração com `WebApplicationFactory` contra um PostgreSQL de verdade: chamam as rotas por HTTP e conferem status, corpo e efeito no banco. Também fiz um teste de mutação manual: quebrei a regra de saldo e confirmei que os testes falham.

**13. Como funcionam as migrations? Como você garante que não esqueceu nenhuma?**
O EF compara o modelo com o snapshot e gera a migration em C#. No CI rodo `dotnet ef migrations has-pending-model-changes`, que falha se o modelo mudou sem migration. Além disso, os testes de integração aplicam as migrations num banco vazio a cada execução.

**14. O que o CI faz?**
Restore, verificação de formatação, build em Release com avisos como erro, checagem de migrations pendentes, testes com um PostgreSQL de serviço e, na main, publica a documentação OpenAPI no GitHub Pages a partir da API rodando. Se uma etapa falhar, o merge fica bloqueado pelo status vermelho.

**15. O que você melhoraria?**
Refresh token e rate limiting no login; custo médio ponderado nas entradas; relatório agregado no SQL em vez de em memória para volumes grandes; exportação em CSV; e suporte a mais de um depósito, com transferência entre eles.

---

## 12. Comandos úteis

```bash
docker compose up -d                                   # banco local
dotnet run --project src/StockCore.Api                 # API em http://localhost:5080/swagger
dotnet test                                            # todos os testes (precisa do banco)
dotnet test tests/StockCore.UnitTests                  # só unitários (sem banco)
dotnet format                                          # corrige formatação
dotnet tool restore                                    # instala o dotnet-ef do manifesto
dotnet ef migrations add NomeDaMudanca --project src/StockCore.Infrastructure --startup-project src/StockCore.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/StockCore.Infrastructure --startup-project src/StockCore.Api
```
