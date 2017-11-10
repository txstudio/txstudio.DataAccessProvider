# txstudio.DataAccessProvider

使用 .NET Standard 撰寫的資料存取操作基底類別

此類別庫目前沒有測試程式碼

## 類別庫支援一覽表

|專案|支援版本|備註|
|--|--|--|
|DataAccessProvider|.NET Standard v2.0|資料存取的基底類別庫|
|DataAccessProvider.SqlClient|.NET Standard v2.0|使用 ADO.NET 進行 MS-SQL 資料庫操作的基底類別|
|DataAccessProvider.OracleClient|Full .NET Framework v4.6.1|使用 ADO.NET 進行 Oracle 資料庫操作的基底類別|

### 注意事項
OracleClient 官方並沒有出 .NET Standard 版本，故 DataAccessProvider.OracleClient 僅支援 .NET 4.6.1 版本
