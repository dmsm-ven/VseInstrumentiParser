using Dapper;
using MySqlConnector;
using System.Data;

namespace VseInstrumentiParser;

public class DbQueryRunner
{
    private readonly string connectionString;

    public DbQueryRunner(string connectionString)
    {
        this.connectionString = connectionString;
    }
    public async Task Execute(string sql)
    {
        //напиши подключение к базе данных mysql через Dapper
        using IDbConnection dbConnection = new MySqlConnection(connectionString);
        await dbConnection.ExecuteAsync(sql);
    }

    public async Task ValidateModelName(string modelName)
    {
        using IDbConnection dbConnection = new MySqlConnection(connectionString);
        var modelsCount = await dbConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM oc_product WHERE model = @ModelName OR sku = @ModelName", new { ModelName = modelName });

        if (modelsCount != 1)
        {
            throw new Exception($"Model name '{modelName}' is not valid. Expected exactly one match in the database, but found {modelsCount}.");
        }

    }
}
