namespace Rcm.Api.Domain;

// Declared from lowest to highest rank, so roles compare by value.
public enum Role
{
    Biller,
    Supervisor,
    Manager,
}
