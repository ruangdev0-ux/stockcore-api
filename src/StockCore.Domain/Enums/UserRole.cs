namespace StockCore.Domain.Enums;

/// <summary>Papéis de acesso. Viewer só consulta; Operator registra entradas e saídas; Admin faz tudo.</summary>
public enum UserRole
{
    Admin,
    Operator,
    Viewer,
}
