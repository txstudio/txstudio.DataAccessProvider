using Oracle.ManagedDataAccess.Client;
using System;
using System.Data.Common;

namespace DataAccessProvider.OracleClient
{
    public abstract class OracleClientProvider : DbClientProvider
    {
        public OracleClientProvider()
            : base(OracleClientFactory.Instance)
        {
            var _cmd = this._DbCommand as OracleCommand;
            _cmd.BindByName = true;
        }

        protected override DbParameter GetDbParameter(string Name, object DbType, int? Size, object Value)
        {
            var _DbParameter = (OracleParameter)this.DbCommand.CreateParameter();

            _DbParameter.ParameterName = Name;

            if (DbType != null)
            {
                _DbParameter.OracleDbType = (OracleDbType)DbType;
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
