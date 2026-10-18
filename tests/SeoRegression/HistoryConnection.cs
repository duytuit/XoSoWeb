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
        }
        else if (CommandText.StartsWith("SELECT v.GameCode"))
        {
            Columns(typeof(string), typeof(string), typeof(DateTime));
            table.Rows.Add("MEGA645", "00019", new DateTime(2024, 10, 17));
        }
        else if (CommandText.Contains("FROM RankedResults"))
        {
            var date = (DateTime)parameters.Parameters["@exactDate"].Value;
            if ((DateTime)parameters.Parameters["@dateEnd"].Value != date.AddDays(1)
                || !CommandText.Contains("DrawDate >= @exactDate AND DrawDate < @dateEnd")) throw new Exception("Historical query lost its date bounds");
            Columns(typeof(long), typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(DateTime));
            if (date == new DateTime(2024, 10, 17) || date == new DateTime(2024, 10, 16))
                table.Rows.Add(1L, parameters.Parameters["@region"].Value, "FX", parameters.Parameters["@provinceName"].Value is string province ? province : "Fixture station", date, "[{\"PrizeName\":\"Test prize\",\"Numbers\":[\"00123\"]}]", date.AddHours(19));
        }
        else if (CommandText.Contains("FROM LatestResults"))
        {
            var draw = (string)parameters.Parameters["@exactDraw"].Value;
            if ((string)parameters.Parameters["@selectedGameCode"].Value != "MEGA645"
                || !CommandText.Contains("DrawNo = @exactDraw")) throw new Exception("Draw query lost its identifier");
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
        else throw new Exception("Unexpected fixture query");
        return table.CreateDataReader();
    }
}
