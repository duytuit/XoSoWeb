using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;
using xsmbsocket.Models;
using xsmbsocket.Services;
using xsmbsocket.Lotterys.Repositories;
using Newtonsoft.Json;
void Check(bool ok, string message) {if(!ok) throw new Exception(message); Console.WriteLine("PASS "+message);}
XoSoResult Row(string code="HCM", string name="TP HCM", string region="Miền Nam", string prize="G8", string number="51") => new() { Region=region, ProvinceCode=code, ProvinceName=name, DrawDate=new DateTime(2020,1,4), Prizes=new() { new() {PrizeName=prize, Numbers=new() {number}} } };
Check(LotteryResultIdentity.Matches(Row(),Row("TPHCM","TP.HCM","MN")),"HCM aliases share identity");
Check(LotteryResultIdentity.Matches(Row("DNG","Đà Nẵng","MT"),Row("DNA","Đà Nẵng","Miền Trung")),"Da Nang aliases share identity");
Check(!LotteryResultIdentity.Matches(Row(),Row("LA","Long An")),"different stations stay separate");
var cfg = new ConfigurationBuilder().SetBasePath(args[0]).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
var cs = new SqlConnectionStringBuilder(cfg.GetConnectionString("DefaultConnection")) {ConnectTimeout=5}.ConnectionString;
await using var connection = new SqlConnection(cs); await connection.OpenAsync();
await connection.ExecuteAsync(@"CREATE TABLE ##XoSoResultsRegression20261025 (Id bigint IDENTITY PRIMARY KEY, Region nvarchar(50), ProvinceCode nvarchar(100), ProvinceName nvarchar(100), DrawDate date, Prizes nvarchar(max), CreatedAt datetime DEFAULT GETDATE(), UpdatedAt datetime DEFAULT GETDATE())");
try {
var repo = new XoSoRepository(cs);
await Task.WhenAll(Enumerable.Range(0,12).Select(i=> i%3==0 ? repo.FillMissingPrizesAsync(Row("TPHCM","TP.HCM","MN"),default) : i%3==1 ? repo.InsertIfMissingAsync(Row(),default) : repo.SaveAsync(Row("HCM","Thành phố Hồ Chí Minh"),default)));
Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ##XoSoResultsRegression20261025")==1,"concurrent writes across all three methods insert one row");
await repo.FillMissingPrizesAsync(Row(prize:"G7",number:"554"),default);
await repo.FillMissingPrizesAsync(Row(number:"99"),default);
var prizes=JsonConvert.DeserializeObject<List<XoSoPrize>>(await connection.ExecuteScalarAsync<string>("SELECT Prizes FROM ##XoSoResultsRegression20261025"));
Check(prizes.Count==2 && prizes.Single(p=>p.PrizeName=="G8").Numbers.Single()=="51","fill missing adds prize and preserves existing value");
await connection.ExecuteAsync("INSERT INTO ##XoSoResultsRegression20261025 (Region,ProvinceCode,ProvinceName,DrawDate,Prizes) SELECT Region,ProvinceCode,ProvinceName,DrawDate,Prizes FROM ##XoSoResultsRegression20261025");
await repo.FillMissingPrizesAsync(Row(prize:"G1",number:"28505"),default);
Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ##XoSoResultsRegression20261025")==2,"existing duplicates do not throw or insert more rows");
Check(!await repo.InsertIfMissingAsync(Row(),default),"insert only reports no insertion for existing draw");
await repo.SaveAsync(Row(number:"52"),default);
prizes=JsonConvert.DeserializeObject<List<XoSoPrize>>(await connection.ExecuteScalarAsync<string>("SELECT TOP 1 Prizes FROM ##XoSoResultsRegression20261025 ORDER BY UpdatedAt DESC,Id DESC"));
Check(prizes.Count==3 && prizes.Single(p=>p.PrizeName=="G8").Numbers.Single()=="52","save updates supplied prize and keeps other prizes");
} finally {await connection.ExecuteAsync("DROP TABLE ##XoSoResultsRegression20261025");}
