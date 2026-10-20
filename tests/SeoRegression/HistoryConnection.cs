using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

// Isolated test fixture. Verifies SQL bounds/parameters and Razor output, not SQL Server execution plans.
public sealed class HistoryConnection : DbConnection
{
    private ConnectionState state;
    public override string ConnectionString { get; set; } = "";
    public override string Database => "fixture";
    public override string DataSource => "fixture";
    public override string ServerVersion => "16.0";
    public override ConnectionState State => state;
    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
    public override void Close() => state = ConnectionState.Closed;
    public override void Open() => state = ConnectionState.Open;
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => new HistoryCommand(this);
}

public sealed class HistoryCommand : DbCommand
{
    private readonly SqlCommand parameters = new SqlCommand();
    public HistoryCommand(DbConnection connection) { DbConnection = connection; }
    public override string CommandText { get; set; }
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
    protected override DbTransaction DbTransaction { get; set; }
    public override void Cancel() { }
    public override int ExecuteNonQuery() => throw new NotSupportedException();
    public override object ExecuteScalar() => throw new NotSupportedException();
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new SqlParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var table = new DataTable();
        void Columns(params Type[] types) { foreach (var type in types) table.Columns.Add("c" + table.Columns.Count, type); }
        if (CommandText.StartsWith("SELECT Region, ProvinceCode"))
        {
            if (CommandText.Contains("ISJSON") || CommandText.Contains("OPENJSON")) throw new Exception("Catalog requires unsupported SQL JSON functions");
            Columns(typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(DateTime), typeof(string));
            foreach (var code in new[] { "FX", "SOURCE-FX" })
                table.Rows.Add("MN", code, "Fixture station", new DateTime(2024, 10, 17), new DateTime(2024, 10, 17, 19, 0, 0), "[{\"PrizeName\":\"Test prize\",\"Numbers\":[\"00123\"]}]");
            foreach (var name in new[] { "TPHCM", "TP HCM" })
                table.Rows.Add("MN", name, name, new DateTime(2024, 10, 17), new DateTime(2024, 10, 17, 19, 0, 0), "[{\"PrizeName\":\"G1\",\"Numbers\":[\"00123\"]}]");
        }
        else if (CommandText.StartsWith("SELECT v.GameCode"))
        {
            Columns(typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(int));
            table.Rows.Add("MEGA645", "00019", new DateTime(2024, 10, 17), "01 07 09", 0);
            table.Rows.Add("KENO", "00999", new DateTime(2024, 10, 17), "01 02", 0);
            table.Rows.Add("BINGO18", "00998", new DateTime(2024, 10, 17), "[]", 0);
        }
        else if (CommandText.Contains("FROM RankedResults"))
        {
            var exact = parameters.Parameters["@exactDate"].Value is DateTime;
            var date = exact ? (DateTime)parameters.Parameters["@exactDate"].Value : new DateTime(2024, 10, 17);
            if (exact && ((DateTime)parameters.Parameters["@dateEnd"].Value != date.AddDays(1)
                || !CommandText.Contains("DrawDate >= @exactDate AND DrawDate < @dateEnd"))) throw new Exception("Historical query lost its date bounds");
            Columns(typeof(long), typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(DateTime));
            if ((string)parameters.Parameters["@region"].Value == "MT" && date == new DateTime(2024, 10, 17))
            {
                table.Rows.Add(10L, "MT", "A", "Fixture A", date, "[{\"PrizeName\":\"Giải Đặc Biệt\",\"Numbers\":[\"001234\"]},{\"PrizeName\":\"Giải 8\",\"Numbers\":[\"01\"]},{\"PrizeName\":\"Giải tư\",\"Numbers\":[\"00123\"]}]", date.AddHours(19));
                table.Rows.Add(11L, "MT", "B", "Fixture B", date, "[{\"PrizeName\":\"G8\",\"Numbers\":[\"02\"]},{\"PrizeName\":\"DB\",\"Numbers\":[\"005678\"]},{\"PrizeName\":\"4\",\"Numbers\":[\"00456\"]}]", date.AddHours(19));
                return table.CreateDataReader();
            }
            if (date == new DateTime(2024, 10, 17) || date == new DateTime(2024, 10, 16))
                table.Rows.Add(1L, parameters.Parameters["@region"].Value, "FX", parameters.Parameters["@provinceName"].Value is string province ? province : "Fixture station", date, "[{\"PrizeName\":\"Test prize\",\"Numbers\":[\"00123\"]}]", date.AddHours(19));
        }
        else if (CommandText.Contains("FROM LatestResults"))
        {
            var draw = parameters.Parameters["@exactDraw"].Value as string ?? "00019";
            if (!CommandText.Contains("DrawNo = @exactDraw")) throw new Exception("Draw query lost its identifier");
            Columns(typeof(long), typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(string), typeof(long), typeof(long), typeof(int), typeof(string), typeof(string), typeof(int));
            if (draw == "00019" || draw == "00020")
                table.Rows.Add(2L, "MEGA645", draw, new DateTime(2024, 10, 17), "01 07 09", "00", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 1);
        }
        else if (CommandText.Contains("FROM dbo.VietlottPrizes"))
        {
            if (!CommandText.Contains("ResultId IN (@id0)") || (long)parameters.Parameters["@id0"].Value != 2L) throw new Exception("Prizes are not batched by result id");
            Columns(typeof(long), typeof(string), typeof(string), typeof(string));
            table.Rows.Add(2L, "Fixture prize", "001 007", "Fixture value");
        }
        else if (CommandText.Contains("SELECT Region, ProvinceName, DrawDate, Prizes"))
        {
            Columns(typeof(string), typeof(string), typeof(DateTime), typeof(string));
            table.Rows.Add("MB", "Fixture station", (DateTime)parameters.Parameters["@startDate"].Value, "[{\"PrizeName\":\"DB\",\"Numbers\":[\"00123\"]}]");
        }
        else if (CommandText.StartsWith("SELECT Region, DrawDate, Prizes"))
        {
            Columns(typeof(string), typeof(DateTime), typeof(string));
            table.Rows.Add(parameters.Parameters["@region"].Value, (DateTime)parameters.Parameters["@start"].Value, "[{\"PrizeName\":\"DB\",\"Numbers\":[\"00123\"]}]");
        }
        else throw new Exception("Unexpected fixture query");
        return table.CreateDataReader();
    }
}
