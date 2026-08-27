namespace NsiTransfer.DAL.Routes;

/// <summary>
/// Набор адресов конечных точек Web API Login.
/// </summary>
public static class ApiLoginRoutes
{
    private static string Path(string value, int version = 1)
    {
        return ApiRoutes.Path($"/login{value}", version);
    }

    /// <summary>
    /// Конечная точка получения списка хранилищ.
    /// </summary>
    public static string StorageDefinitions => Path("/storage-definitions");

    /// <summary>
    /// Конечная точка входа.
    /// </summary>
    public static string SignIn => Path("/sign-in");

    /// <summary>
    /// Конечная точка выхода.
    /// </summary>
    public static string SignOut => Path("/sign-out");

    /// <summary>
    /// Конечная точка обновление токена.
    /// </summary>
    public static string UpdateToken => Path("/update-token");

    /// <summary>
    /// Конечная точка получения информации о текущем пользователе
    /// </summary>
    public static string CurrentUserInfo => Path("/current-user-info");
}
