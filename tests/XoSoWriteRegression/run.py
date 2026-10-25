"""Run against an isolated global temporary SQL table; never writes dbo.XoSoResults.
Usage: python3 tests/XoSoWriteRegression/run.py
Requires sibling xsmbsocket checkout and the configured SQL Server connection.
"""
from pathlib import Path
import subprocess
import tempfile
import uuid
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parents[2]
service = root.parent / "xsmbsocket"
with tempfile.TemporaryDirectory(prefix="xoso-write-regression-") as directory:
    scratch = Path(directory)
    table = "##XoSoResultsRegression" + uuid.uuid4().hex
    repository = service / "Lotterys/Repositories/XoSoRepository.cs"
    (scratch / "XoSoRepository.cs").write_text(repository.read_text().replace("dbo.XoSoResults", table))
    (scratch / "Program.cs").write_text((Path(__file__).parent / "Program.cs").read_text().replace("##XoSoResultsRegression20261025", table))
    sources = [service / p for p in ["Services/LotteryResultIdentity.cs", "Services/LotteryPrizeMerger.cs", "Models/XoSoResult.cs", "Models/XoSoPrize.cs"]]
    includes = "".join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in sources)
    (scratch / "tests.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net6.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><PackageReference Include="Dapper" Version="2.1.79"/><PackageReference Include="Newtonsoft.Json" Version="13.0.4"/><PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="6.0.36"/><PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="6.0.0"/>''' + includes + '</ItemGroup></Project>')
    subprocess.run(["dotnet", "run", "--project", str(scratch / "tests.csproj"), "--", str(root)], check=True)
