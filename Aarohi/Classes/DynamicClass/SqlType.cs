namespace Aarohi.Classes
{
    /// <summary>
    /// Specifies the target database engine for <see cref="DynamicClass"/>.
    /// </summary>
    public enum SqlType
    {
        /// <summary>
        /// Microsoft SQL Server (default).
        /// </summary>
        SqlServer = 0,

        /// <summary>
        /// SQLite database engine.
        /// </summary>
        Sqlite = 1,

        /// <summary>
        /// MySQL database engine.
        /// </summary>
        MySql = 2,

        /// <summary>
        /// PostgreSQL database engine.
        /// </summary>
        PostgreSql = 3
    }
}
