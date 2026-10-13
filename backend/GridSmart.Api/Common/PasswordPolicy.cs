namespace GridSmart.Api.Common;

/// <summary>Quy tac mat khau: it nhat 8 ky tu, co ca chu va so.</summary>
public static class PasswordPolicy
{
    public static void Ensure(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new DomainException(ErrorCodes.ValidationFailed, "Mật khẩu cần ít nhất 8 ký tự, gồm cả chữ và số.");
    }
}
