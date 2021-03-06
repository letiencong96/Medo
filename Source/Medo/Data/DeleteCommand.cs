//Josip Medved <jmedved@jmedved.com>   www.medo64.com

//2012-01-11: Refactoring.
//2011-08-04: Workaround mono bug #500987.
//2008-04-10: Uses IFormatProvider.
//2008-02-29: Fixed bugs in debug mode.
//2008-02-21: Initial version.


using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Medo.Data {

    /// <summary>
    /// Generating command objects based on SQL queries or stored procedures.
    /// </summary>
    public class DeleteCommand : IDbCommand {

        private string _tableName;
        private bool _needsMonoFix; //Mono bug #500987 / Error converting data type varchar to datetime


        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="connection">A connection object.</param>
        /// <param name="tableName">Name of table.</param>
        /// <exception cref="System.ArgumentNullException">Connection cannot be null.</exception>
        /// <exception cref="System.ArgumentException">Table name cannot be empty or null.</exception>
        /// <exception cref="System.InvalidCastException">Column name should be string and non-null.</exception>
        public DeleteCommand(IDbConnection connection, string tableName) {
            if (connection == null) { throw new ArgumentNullException("connection", Resources.ExceptionConnectionCannotBeNull); }
            if (string.IsNullOrEmpty(tableName)) { throw new ArgumentException(Resources.ExceptionTableNameCannotBeEmptyOrNull, "tableName"); }

            this._baseCommand = connection.CreateCommand();

            this._tableName = tableName;

            UpdateCommandText();
        }



        private string _whereText;
        private List<IDbDataParameter> _whereParameters = new List<IDbDataParameter>();

        /// <summary>
        /// Sets where statement used.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An System.Object array containing zero or more objects to format. Those objects are inserted in Parameters as IDbDataParameter with name of @Px where x is order index.</param>
        public void SetWhere(string format, params object[] args) {
            if (this._whereParameters != null) {
                for (int i = 0; i < this._whereParameters.Count; ++i) {
                    this._baseCommand.Parameters.Remove(this._whereParameters[i]);
                }
            }

            this._needsMonoFix = false;
            List<string> argList = new List<string>();
            if (args != null) {
                for (int i = 0; i < args.Length; ++i) {
                    if (args[i] == null) {
                        argList.Add("NULL");
                    } else {
                        string paramName = string.Format(CultureInfo.InvariantCulture, "@W{0}", i);
                        argList.Add(paramName);
                        var param = this._baseCommand.CreateParameter();
                        param.ParameterName = paramName;
                        if ((args[i] is DateTime) && (IsRunningOnMono)) {
                            args[i] = ((DateTime)args[i]).ToString(CultureInfo.InvariantCulture);
                            this._needsMonoFix = true;
                        }
                        param.Value = args[i];
                        if (param.DbType == DbType.DateTime) {
                            OleDbParameter odp = param as OleDbParameter;
                            if (odp != null) { odp.OleDbType = OleDbType.Date; }
                        }
                        this._whereParameters.Add(param);
                        this._baseCommand.Parameters.Add(param);
                    }
                }
            }
            if (argList.Count > 0) {
                this._whereText = string.Format(CultureInfo.InvariantCulture, format, argList.ToArray());
            } else {
                this._whereText = format;
            }

            UpdateCommandText();
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Proper parameterization is done in code.")]
        private void UpdateCommandText() {
            if (string.IsNullOrEmpty(this._whereText)) {
                if ((this.Connection is SqlConnection) && this._needsMonoFix) {
                    this._baseCommand.CommandText = string.Format(CultureInfo.InvariantCulture, "SET LANGUAGE us_english; DELETE FROM {0};", this._tableName);
                } else {
                    this._baseCommand.CommandText = string.Format(CultureInfo.InvariantCulture, "DELETE FROM {0};", this._tableName);
                }
            } else {
                if ((this.Connection is SqlConnection) && this._needsMonoFix) {
                    this._baseCommand.CommandText = string.Format(CultureInfo.InvariantCulture, "SET LANGUAGE us_english; DELETE FROM {0} WHERE {1};", this._tableName, _whereText.ToString());
                } else {
                    this._baseCommand.CommandText = string.Format(CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1};", this._tableName, _whereText.ToString());
                }
            }
        }


        #region Base properties

        private IDbCommand _baseCommand;
        /// <summary>
        /// Gets underlying connection.
        /// </summary>
        public IDbCommand BaseCommand {
            get { return this._baseCommand; }
        }

        #endregion

        #region IDbCommand Members

        /// <summary>
        /// Attempts to cancels the execution of an System.Data.IDbCommand.
        /// </summary>
        public void Cancel() {
            this._baseCommand.Cancel();
        }

        /// <summary>
        /// Gets or sets the text command to run against the data source.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Proper parameterization is done in code.")]
        public String CommandText {
            get { return this._baseCommand.CommandText; }
            set { this._baseCommand.CommandText = value; }
        }

        /// <summary>
        /// Gets or sets the wait time before terminating the attempt to execute a command and generating an error.
        /// </summary>
        public Int32 CommandTimeout {
            get { return this._baseCommand.CommandTimeout; }
            set { this._baseCommand.CommandTimeout = value; }
        }

        /// <summary>
        /// Indicates or specifies how the System.Data.IDbCommand.CommandText property is interpreted.
        /// </summary>
        public CommandType CommandType {
            get { return this._baseCommand.CommandType; }
            set { this._baseCommand.CommandType = value; }
        }

        /// <summary>
        /// Gets or sets the System.Data.IDbConnection used by this instance of the System.Data.IDbCommand.
        /// </summary>
        public IDbConnection Connection {
            get { return this._baseCommand.Connection; }
            set { this._baseCommand.Connection = value; }
        }

        /// <summary>
        /// Creates a new instance of an System.Data.IDbDataParameter object.
        /// </summary>
        public IDbDataParameter CreateParameter() {
            return this._baseCommand.CreateParameter();
        }

        /// <summary>
        /// Executes an SQL statement against the Connection object of a .NET Framework data provider, and returns the number of rows affected.
        /// </summary>
        public Int32 ExecuteNonQuery() {
#if DEBUG
            DebugCommand();
#endif
            return this._baseCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes the System.Data.IDbCommand.CommandText against the System.Data.IDbCommand.Connection, and builds an System.Data.IDataReader using one of the System.Data.CommandBehavior values.
        /// </summary>
        /// <param name="behavior">One of the System.Data.CommandBehavior values.</param>
        public IDataReader ExecuteReader(CommandBehavior behavior) {
#if DEBUG
            DebugCommand();
#endif
            return this._baseCommand.ExecuteReader(behavior);
        }

        /// <summary>
        /// Executes the System.Data.IDbCommand.CommandText against the System.Data.IDbCommand.Connection and builds an System.Data.IDataReader.
        /// </summary>
        public IDataReader ExecuteReader() {
#if DEBUG
            DebugCommand();
#endif
            return this._baseCommand.ExecuteReader();
        }

        /// <summary>
        /// Executes the query, and returns the first column of the first row in the resultset returned by the query. Extra columns or rows are ignored.
        /// </summary>
        public Object ExecuteScalar() {
#if DEBUG
            DebugCommand();
#endif
            return this._baseCommand.ExecuteScalar();
        }

        /// <summary>
        /// Gets the System.Data.IDataParameterCollection.
        /// </summary>
        public IDataParameterCollection Parameters {
            get { return this._baseCommand.Parameters; }
        }

        /// <summary>
        /// Creates a prepared (or compiled) version of the command on the data source.
        /// </summary>
        public void Prepare() {
            this._baseCommand.Prepare();
        }

        /// <summary>
        /// Gets or sets the transaction within which the Command object of a .NET Framework data provider executes.
        /// </summary>
        public IDbTransaction Transaction {
            get { return this._baseCommand.Transaction; }
            set { this._baseCommand.Transaction = value; }
        }

        /// <summary>
        /// Gets or sets how command results are applied to the System.Data.DataRow when used by the System.Data.IDataAdapter.Update(System.Data.DataSet) method of a System.Data.Common.DbDataAdapter.
        /// </summary>
        public UpdateRowSource UpdatedRowSource {
            get { return this._baseCommand.UpdatedRowSource; }
            set { this._baseCommand.UpdatedRowSource = value; }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">True if managed resources should be disposed; otherwise, false.</param>
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                this._baseCommand.Dispose();
            }
        }

        #endregion


#if DEBUG
        private void DebugCommand() {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "-- {0}", this._baseCommand.CommandText);
            for (int i = 0; i < this._baseCommand.Parameters.Count; ++i) {
                sb.AppendLine();
                System.Data.Common.DbParameter curr = this._baseCommand.Parameters[i] as System.Data.Common.DbParameter;
                if (curr != null) {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "--     {0}=\"{1}\" ({2})", curr.ParameterName, curr.Value, curr.DbType);
                } else {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "--     {0}", this._baseCommand.Parameters[i].ToString());
                }
            }
            Debug.WriteLine(sb.ToString());
        }
#endif

        private static class Resources {

            internal static string ExceptionConnectionCannotBeNull { get { return "Connection cannot be null."; } }

            internal static string ExceptionTableNameCannotBeEmptyOrNull { get { return "Table name cannot be empty or null."; } }

        }


        private static bool IsRunningOnMono {
            get {
                return (Type.GetType("Mono.Runtime") != null);
            }
        }

    }

}
