# Database

## SQL Server

Database:

xoso

## VietlottResults
CREATE TABLE dbo.VietlottResults
(
    Id BIGINT IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_VietlottResults PRIMARY KEY,

    GameCode VARCHAR(30) NOT NULL,

    DrawNo VARCHAR(30) NOT NULL,

    DrawDate DATE NOT NULL,

    Numbers NVARCHAR(1000) NULL,

    SpecialNumbers NVARCHAR(500) NULL,

    Total INT NULL,

    OddEven NVARCHAR(100) NULL,

    Size NVARCHAR(100) NULL,

    CreatedAt DATETIME NOT NULL
        CONSTRAINT DF_VietlottResults_CreatedAt
        DEFAULT GETDATE()
);
## VietlottPrizes
CREATE TABLE dbo.VietlottPrizes
(
    Id BIGINT IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_VietlottPrizes PRIMARY KEY,

    ResultId BIGINT NOT NULL,

    PrizeName NVARCHAR(100) NOT NULL,

    Numbers NVARCHAR(2000) NULL,

    PrizeValue NVARCHAR(200) NULL,

    CreatedAt DATETIME NOT NULL
        CONSTRAINT DF_VietlottPrizes_CreatedAt
        DEFAULT GETDATE()
);
==========================================
GAME       : "LOTTO535"
DRAW       : #"00883"
DATE       : "13/09/2026"
NUMBERS    : "01 07 29 32 34"
SPECIAL    : "02"
==========================================
DB SAVED: "LOTTO535" #"00883" -> ID=26767
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "POWER655"
DRAW       : #"01397"
DATE       : "12/09/2026"
NUMBERS    : "07 24 31 43 47 54"
SPECIAL    : "22"
==========================================
DB SAVED: "POWER655" #"01397" -> ID=26585
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "MEGA645"
DRAW       : #"01562"
DATE       : "13/09/2026"
NUMBERS    : "04 12 31 34 38 41"
==========================================
DB SAVED: "MEGA645" #"01562" -> ID=26862
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "BINGO18"
DRAW       : #"0186325"
DATE       : "13/09/2026"
NUMBERS    : "1 4 4"
TOTAL      : 9
SIZE       : "Nhỏ"
==========================================
DB SAVED: "BINGO18" #"0186325" -> ID=26869
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "BINGO18"
DRAW       : #"0186324"
DATE       : "13/09/2026"
NUMBERS    : "3 2 5"
TOTAL      : 10
SIZE       : "Hòa"
==========================================
DB SAVED: "BINGO18" #"0186324" -> ID=26867
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "BINGO18"
DRAW       : #"0186323"
DATE       : "13/09/2026"
NUMBERS    : "4 1 2"
TOTAL      : 7
SIZE       : "Nhỏ"
==========================================
DB SAVED: "BINGO18" #"0186323" -> ID=26865
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "KENO"
DRAW       : #"0295593"
DATE       : "13/09/2026"
NUMBERS    : "03 10 15 18 23 26 27 31 37 40 54 60 62 64 70 72 73 75 79 80"
ODD/EVEN   : "Chẵn (11)"
SIZE       : "Hòa"
==========================================
DB SAVED: "KENO" #"0295593" -> ID=26868
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "KENO"
DRAW       : #"0295592"
DATE       : "13/09/2026"
NUMBERS    : "02 06 07 11 12 17 20 25 35 36 37 54 55 57 58 63 64 70 73 76"
ODD/EVEN   : "Hòa"
SIZE       : "Nhỏ (11)"
==========================================
DB SAVED: "KENO" #"0295592" -> ID=26866
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "KENO"
DRAW       : #"0295591"
DATE       : "13/09/2026"
NUMBERS    : "07 09 12 13 15 16 18 20 21 26 31 33 38 44 56 58 69 73 77 80"
ODD/EVEN   : "Hòa"
SIZE       : "Nhỏ (13)"
==========================================
DB SAVED: "KENO" #"0295591" -> ID=26864
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "MAX3D"
DRAW       : #"01131"
DATE       : "11/09/2026"
PRIZES:
  "Giải ba": "245, 848, 549, 592, 805, 160, 944, 314" - "100 nghìn"
  "Giải nhì": "380, 710, 853, 891, 661, 460" - "210 nghìn"
  "Giải Nhất": "526, 486, 133, 575" - "350 nghìn"
  "Giải Đặc biệt": "2, 9, 1, 8, 1, 4" - "1 triệu"
==========================================
DB SAVED: "MAX3D" #"01131" -> ID=26305
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "MAX3D_PLUS"
DRAW       : #"01131"
DATE       : "11/09/2026"
PRIZES:
  "Giải sáu": "526, 486, 133, 575, 380, 710, 853, 891, 661, 460, 245, 848, 549, 592, 805, 160, 944, 314" - "40 nghìn"
  "Giải năm": "291, 814" - "150 nghìn"
  "Giải tư": "291, 814, 526, 486, 133, 575, 380, 710, 853, 891, 661, 460, 245, 848, 549, 592, 805, 160, 944, 314" - "1 triệu"
  "Giải ba": "245, 848, 549, 592, 805, 160, 944, 314" - "5 triệu"
  "Giải nhì": "380, 710, 853, 891, 661, 460" - "10 triệu"
  "Giải Nhất": "526, 486, 133, 575" - "40 triệu"
  "Giải Đặc biệt": "2, 9, 1, 8, 1, 4, 8, 1, 4, 2, 9, 1" - "1 tỷ"
==========================================
DB SAVED: "MAX3D_PLUS" #"01131" -> ID=26306
2026-09-13 18:46:44 [Information] 

==========================================
GAME       : "MAX3D_PRO"
DRAW       : #"00778"
DATE       : "12/09/2026"
PRIZES:
  "Giải sáu": "057, 051, 535, 178, 859, 823, 578, 280, 701, 265, 270, 800, 687, 999, 976, 713, 629, 625" - "40 nghìn"
  "Giải năm": "782, 178" - "100 nghìn"
  "Giải tư": "782, 178, 057, 051, 535, 178, 859, 823, 578, 280, 701, 265, 270, 800, 687, 999, 976, 713, 629, 625" - "1 triệu"
  "Giải Ba": "270, 800, 687, 999, 976, 713, 629, 625" - "4 triệu"
  "Giải Nhì": "859, 823, 578, 280, 701, 265" - "10 triệu"
  "Giải nhất": "057, 051, 535, 178" - "30 triệu"
==========================================
DB SAVED: "MAX3D_PRO" #"00778" -> ID=26588
------------------------------------------
## XoSoResults

CREATE TABLE dbo.XoSoResults
(
    Id BIGINT IDENTITY(1,1) NOT NULL
        CONSTRAINT PK_XoSoResults PRIMARY KEY,

    Region VARCHAR(10) NOT NULL,           -- MB / MT / MN
    ProvinceCode VARCHAR(30) NOT NULL,     -- MB, AG, KG...
    ProvinceName NVARCHAR(100) NOT NULL,

    DrawDate DATE NOT NULL,

    -- Lưu toàn bộ giải dưới dạng JSON
    Prizes NVARCHAR(MAX) NOT NULL,

    CreatedAt DATETIME NOT NULL
        CONSTRAINT DF_XoSoResults_CreatedAt DEFAULT GETDATE(),

    UpdatedAt DATETIME NOT NULL
        CONSTRAINT DF_XoSoResults_UpdatedAt DEFAULT GETDATE(),
);
Id	Region	ProvinceCode	ProvinceName	DrawDate	Prizes	CreatedAt	UpdatedAt
6	Miền bac	Xổ số Miền Bắc	Xổ số Miền Bắc	2026-09-13	[{"PrizeName":"Giải ĐB","Numbers":["83799"]},{"PrizeName":"Giải nhất","Numbers":["63029"]},{"PrizeName":"Giải nhì","Numbers":["21509","71228"]},{"PrizeName":"Giải ba","Numbers":["28530","12732","41085","43205","58675","62527"]},{"PrizeName":"Giải tư","Numbers":["2812","7409","9962","7240"]},{"PrizeName":"Giải năm","Numbers":["5439","8360","4126","2579","5130","5884"]},{"PrizeName":"Giải sáu","Numbers":["988","648","462"]},{"PrizeName":"Giải bảy","Numbers":["21","88","40","27"]}]	2026-09-13 18:51:27.160	2026-09-13 18:51:27.277