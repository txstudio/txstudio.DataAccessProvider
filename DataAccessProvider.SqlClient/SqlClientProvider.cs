using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace DataAccessProvider.SqlClient
{
    public abstract class SqlClientProvider : DbClientProvider
    {
        public SqlClientProvider()
            : base(SqlClientFactory.Instance)
        {
            var _cmd = this._DbCommand as SqlCommand;
        }

        protected override DbParameter GetDbParameter(string Name, object DbType, int? Size, object Value)
        {
            var _DbParameter = (SqlParameter)this.DbCommand.CreateParameter();

            _DbParameter.ParameterName = Name;

            if (DbType != null)
            {
                _DbParameter.SqlDbType = (SqlDbType)DbType;
            }

            if (Size.HasValue == true)
            {
                _DbParameter.Size = Size.Value;
            }

            _DbParameter.Value = Value;

            return _DbParameter;
        }
    }
}
