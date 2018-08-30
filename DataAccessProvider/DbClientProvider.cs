using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace DataAccessProvider
{
    public abstract class DbClientProvider : IDisposable
    {

        private readonly DbProviderFactory _factory;

        private readonly DbConnection _DbConnection;
        protected readonly DbCommand _DbCommand;

        private string _ConnectionString;
        private DbDataReader _DbReader;
        private DbDataAdapter _DbAdapter;

        private DbTransaction _DbTransaction;


        public DbClientProvider(DbProviderFactory DbFactory)
        {
            this._factory = DbFactory;
            this._ConnectionString = this.GetConnectionString();

            this._DbConnection = _factory.CreateConnection();
            this._DbConnection.ConnectionString = this._ConnectionString;

            this._DbCommand = _factory.CreateCommand();
            this._DbCommand.Connection = this._DbConnection;

            this._DbAdapter = this._factory.CreateDataAdapter();

            this._DbConnection.Open();
        }

        protected abstract string GetConnectionString();



        /// <summary>開啟交易</summary>
        public void BeginTransaction()
        {
            if (this._DbConnection.State != ConnectionState.Open)
            {
                this._DbConnection.Open();
            }
            this._DbTransaction = this._DbConnection.BeginTransaction();
            this._DbCommand.Transaction = this._DbTransaction;
        }

        /// <summary>使用指定的交易隔離設定開啟交易</summary>
        /// <param name="IsolationLevel">交易隔離</param>
        public void BeginTransaction(IsolationLevel IsolationLevel)
        {
            this._DbConnection.Open();
            this._DbTransaction = this._DbConnection.BeginTransaction(IsolationLevel);
            this._DbCommand.Transaction = this._DbTransaction;
        }


        /// <summary>確認交易處理，此方法會關閉資料庫連線</summary>
        public void CommitTransaction()
        {
            if (this._DbTransaction == null)
            {
                throw new NullReferenceException("尚未啟用交易 DbTransaction Is Nothing");
            }

            this._DbTransaction.Commit();
            this._DbConnection.Close();

            this._DbTransaction = null;
        }

        /// <summary>還原交易處理，此方法會關閉資料庫連線</summary>
        public void RollbackTransaction()
        {
            if (this._DbTransaction == null)
            {
                throw new NullReferenceException("尚未啟用交易 DbTransaction Is Nothing");
            }

            this._DbTransaction.Rollback();
            this._DbConnection.Close();

            this._DbTransaction = null;
        }

        /// <summary>開啟資料庫連線</summary>

        private void OpenConnection()
        {
            //若為交易處理階段，資料庫連線已經開啟
            if (this._DbTransaction == null)
            {
                if (this._DbConnection.State == ConnectionState.Closed)
                {
                    this._DbConnection.Open();
                }
            }

        }

        /// <summary>關閉資料庫連線</summary>

        private void CloseConnection()
        {
            //若為交易處理階段，不需要關閉資料庫連線
            if (this._DbTransaction == null)
            {
                if (this._DbConnection.State == ConnectionState.Open)
                {
                    this._DbConnection.Close();
                }
            }

        }

        /// <summary>設定 DbCommand 為 SQL 字串查詢</summary>
        /// <param name="CommandText">SQL 查詢字串</param>
        /// <remarks></remarks>
        public void SetSqlString(string CommandText)
        {
            this._DbCommand.CommandText = CommandText;
            this._DbCommand.CommandType = CommandType.Text;
        }

        /// <summary>設定 DbCommand 為 StoredProcedure 查詢</summary>
        /// <param name="StoreProcName">StoredProcedure 名稱</param>
        /// <remarks></remarks>
        public void SetStoreProcedure(string StoreProcName)
        {
            this._DbCommand.CommandText = StoreProcName;
            this._DbCommand.CommandType = CommandType.StoredProcedure;
        }


        /// <summary>
        /// 將 IDataReader 物件轉換成指定泛型物件集合
        /// </summary>
        /// <typeparam name="T">泛型物件</typeparam>
        /// <param name="DataReader">IDataReader 物件</param>
        /// <returns>轉換後的集合物件</returns>
        private IEnumerable<T> DataReaderMapper<T>(IDataReader DataReader)
        {

            List<T> Items;
            T Template;

            List<string> Columns;
            string ColumnName;

            Columns = new List<string>();
            Items = new List<T>();

            while (DataReader.Read())
            {
                Template = Activator.CreateInstance<T>();

                //先取得 DataReader 的欄位名稱清單
                if ((Columns.Count == 0))
                {
                    for (int index = 0; index <= DataReader.FieldCount - 1; index++)
                    {
                        Columns.Add(DataReader.GetName(index));
                    }
                }


                foreach (PropertyInfo PropertyInfo in Template.GetType().GetProperties())
                {
                    ColumnName = string.Empty;
                    ColumnName = PropertyInfo.Name;

                    //屬性名稱並不存在於 DataReader 的欄位中，處理下一個屬性
                    if (Columns.Contains(ColumnName) == false)
                    {
                        continue;
                    }

                    //指定資料行為 DBNull 的話不設定類別物件屬性值

                    if (Object.Equals(DataReader[ColumnName], DBNull.Value) == false)
                    {
                        PropertyInfo.SetValue(Template, DataReader[ColumnName], null);
                    }
                }

                Items.Add(Template);
            }

            return Items;
        }

        /// <summary>執行資料庫查詢並將查詢結果轉換成指定型態的物件</summary>
        /// <typeparam name="T">指定物件型態</typeparam>
        /// <returns>指定型態的物件</returns>
        public T ExecuteAsMapperSingle<T>()
        {

            IEnumerable<T> Results;
            T Result;

            Results = this.ExecuteAsMapper<T>();
            Result = Results.FirstOrDefault();

            return Result;

        }


        /// <summary>執行資料庫查詢並將查詢結果轉換成 DataTable 物件</summary>
        /// <returns>查詢結果</returns>
        public DataTable ExecuteAsTable()
        {

            DataTable _Result;

            _Result = new DataTable();

            this.SetEmptyDbParameter();
            this._DbReader = this._DbCommand.ExecuteReader();
            _Result.Load(this._DbReader);

            return _Result;

        }

        /// <summary>執行資料庫查詢並將查詢結果轉換成指定型態的物件集合</summary>
        /// <typeparam name="T">指定物件型態</typeparam>
        /// <returns>指定型態物件集合</returns>
        public IEnumerable<T> ExecuteAsMapper<T>()
        {

            IEnumerable<T> Results;

            this.SetEmptyDbParameter();
            this._DbReader = this._DbCommand.ExecuteReader();
            Results = this.DataReaderMapper<T>(this._DbReader);

            return Results;

        }


        /// <summary>執行資料庫查詢並回傳第一個資料列的第一行內容</summary>
        /// <returns>第一個資料列的第一行內容</returns>
        public object ExecuteScalar()
        {

            object Value;

            this.SetEmptyDbParameter();
            Value = this._DbCommand.ExecuteScalar();

            return Value;

        }


        /// <summary>執行資料庫查詢並回傳受影響的資料列數</summary>
        /// <returns>受影響的資料列數</returns>
        public int ExecuteNonQuery()
        {

            int effectRow;

            this.SetEmptyDbParameter();
            effectRow = this._DbCommand.ExecuteNonQuery();

            return effectRow;

        }


        /// <summary>取得 DbParameter 實作物件</summary>
        /// <param name="Name">參數名稱</param>
        /// <param name="DbType">參數類型</param>
        /// <param name="Size">長度</param>
        /// <param name="Value">數值</param>
        /// <returns></returns>
        protected virtual DbParameter GetDbParameter(string Name, object DbType, Nullable<int> Size, object Value)
        {

            var DbParamter = _DbCommand.CreateParameter();

            DbParamter.ParameterName = Name;

            if (DbType != null)
            {
                DbParamter.DbType = (DbType)DbType;
            }

            if (Size.HasValue == true)
            {
                DbParamter.Size = Size.Value;
            }

            DbParamter.Value = Value;

            return DbParamter;

        }

        /// <summary>將沒有資料或空字串的 DbParamter.Value 設定為 DBNull.Value</summary>
        /// <remarks></remarks>

        private void SetEmptyDbParameter()
        {
            var DbParameters = this._DbCommand.Parameters;
            string Value;

            if (DbParameters == null)
            {
                return;
            }


            foreach (DbParameter DbParameter in DbParameters)
            {
                if (DbParameter == null)
                {
                    DbParameter.Value = DBNull.Value;
                    continue;
                }

                if (DbParameter.Value == null)
                {
                    DbParameter.Value = DBNull.Value;
                    continue;
                }

                Value = DbParameter.Value.ToString();
                Value = Value.Trim();

                if (string.IsNullOrWhiteSpace(Value) == true)
                {
                    DbParameter.Value = DBNull.Value;
                }

            }

        }



        /// <summary>取得指定名稱的資料庫參數物件</summary>
        /// <param name="Name">資料庫參數名稱</param>
        /// <returns>資料庫參數物件</returns>
        public DbParameter GetParameter(string Name)
        {
            DbParameter Parameter;

            Parameter = this._DbCommand.Parameters[Name];

            return Parameter;
        }

        /// <summary>取得指定索引的資料庫參數物件</summary>
        /// <param name="Index">資料庫參數索引</param>
        /// <returns>資料庫參數物件</returns>
        public DbParameter GetParameter(int Index)
        {
            DbParameter Parameter;

            Parameter = this._DbCommand.Parameters[Index];

            return Parameter;
        }


        /// <summary>取得指定名稱的資料庫參數物件的數值</summary>
        /// <param name="Name">資料庫參數名稱</param>
        /// <returns>數值</returns>
        public object GetParameterValue(string Name)
        {
            object Value;

            Value = this._DbCommand.Parameters[Name].Value;

            return Value;
        }

        /// <summary>取得指定索引的資料庫參數物件的數值</summary>
        /// <param name="Index">資料庫參數索引</param>
        /// <returns>數值</returns>
        public object GetParameterValue(int Index)
        {
            object Value;

            Value = this._DbCommand.Parameters[Index].Value;

            return Value;
        }

        /// <summary>設定指定索引的資料庫參數數值</summary>
        /// <param name="Index">資料庫參數索引</param>
        /// <returns>數值</returns>
        public void SetParameterValue(int index, object value)
        {
            this.GetParameter(index).Value = value;
        }

        /// <summary>設定指定名稱的資料庫參數數值</summary>
        /// <param name="Name">資料庫參數名稱</param>
        /// <returns>數值</returns>
        public void SetParameterValue(string name, object value)
        {
            this.GetParameter(name).Value = value;
        }


        /// <summary>清空資料庫查詢參數物件</summary>

        public void ClearParameters()
        {
            this._DbCommand.Parameters.Clear();

        }


        /// <summary>移除指定名稱的資料庫參數物件的數值</summary>
        /// <param name="Name">資料庫參數名稱</param>
        public void RemoveParameter(string Name)
        {
            object DbParamter = this.GetParameter(Name);
            this._DbCommand.Parameters.Remove(DbParamter);
        }

        /// <summary>移除指定索引的資料庫參數物件的數值</summary>
        /// <param name="Index">資料庫參數索引</param>
        public void RemoveParameter(int Index)
        {
            object DbParamter = this.GetParameter(Index);
            this._DbCommand.Parameters.Remove(DbParamter);
        }

        /// <summary>加入資料庫查詢參數物件</summary>
        /// <param name="Name">名稱</param>
        /// <param name="DbType">型態</param>
        /// <param name="Size">長度</param>
        /// <param name="Value">數值</param>

        public void AddParameter(string Name, object DbType, Nullable<int> Size, object Value)
        {
            object DbParameter = this.GetDbParameter(Name, DbType, Size, Value);
            this._DbCommand.Parameters.Add(DbParameter);

        }

        /// <summary>加入資料庫查詢參數物件-僅指定名稱與數值</summary>
        /// <param name="Name">名稱</param>
        /// <param name="Value">數值</param>
        public void AddParameter(string Name, object Value)
        {
            this.AddParameter(Name, null, null, Value);
        }

        /// <summary>加入資料庫輸入查詢參數物件</summary>
        /// <param name="Name">名稱</param>
        /// <param name="DbType">型態</param>
        /// <param name="Size">長度</param>
        /// <param name="Value">數值</param>

        public void AddInParameter(string Name, object DbType, Nullable<int> Size, object Value)
        {
            var DbParameter = this.GetDbParameter(Name, DbType, Size, Value);
            DbParameter.Direction = ParameterDirection.Input;

            this._DbCommand.Parameters.Add(DbParameter);

        }

        /// <summary>加入資料庫輸入查詢參數物件-僅指定名稱與數值</summary>
        /// <param name="Name">名稱</param>
        /// <param name="Value">數值</param>
        public void AddInParameter(string Name, object Value)
        {
            this.AddInParameter(Name, null, null, Value);
        }

        /// <summary>加入資料庫輸出查詢參數物件</summary>
        /// <param name="Name">名稱</param>
        /// <param name="DbType">型態</param>
        /// <param name="Size">長度</param>
        /// <param name="Value">數值</param>

        public void AddOutParameter(string Name, object DbType, Nullable<int> Size, object Value)
        {
            var DbParameter = this.GetDbParameter(Name, DbType, Size, Value);
            DbParameter.Direction = ParameterDirection.Output;

            this._DbCommand.Parameters.Add(DbParameter);

        }

        /// <summary>加入資料庫輸出查詢參數物件-僅指定名稱與數值</summary>
        /// <param name="Name">名稱</param>
        /// <param name="Value">數值</param>
        public void AddOutParameter(string Name, object Value)
        {
            this.AddOutParameter(Name, null, null, Value);
        }


        public void Dispose()
        {
            if (this._DbConnection.State == ConnectionState.Closed)
            {
            }
            else
            {
                this._DbConnection.Close();
            }
        }


        /// <summary>取得資料庫連線字串</summary>
        public string ConnectionString
        {
            get { return this._ConnectionString; }
        }

        /// <summary>取得資料庫 DbConnection 物件</summary>
        public DbConnection DbConnection
        {
            get { return this._DbConnection; }
        }

        /// <summary>取得資料庫的 DbCommand 物件</summary>
        public DbCommand DbCommand
        {
            get { return this._DbCommand; }
        }

        /// <summary>取得資料庫的 DbDataAdapter 物件</summary>
        public DbDataAdapter DbDataAdapter
        {
            get { return this._DbAdapter; }
        }


    }
}
