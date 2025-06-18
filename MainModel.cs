using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace FDITools;

public class MainModel : Model
{
    public MainModel()
    {
        Load();
    }

    private string _ServerName = @"localhost\sqlexpress";
    private string _DatabaseName = "fdi_welimr23u1";
    private string _PipelineName = "GWFPF1";

    private int _NumNavigationPoints;
    private int _NumRoutePoints;
    private ObservableCollection<PipelineStats> _PipelineStats = [];

    public int NumNavigationPoints
    {
        get => _NumNavigationPoints;
        set
        {
            _NumNavigationPoints = value;
            NotifyPropertyChanged();
        }
    }

    public int NumRoutePoints
    {
        get => _NumRoutePoints;
        set
        {
            _NumRoutePoints = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand RunCommand => new Command(async () => await Run());

    public Action<double[], double[], Color>? CreatePlotAction { get; set; }

    private async Task Run()
    {
        if (CreatePlotAction is null)
        {
            return;
        }

        using (var conn = CreateConnection())
        {
            var pipelineID = await conn.QueryFirstAsync<Guid>("SELECT ID FROM Pipelines WHERE Name = @PipelineName", new { PipelineName = _PipelineName });
            var pipelineTimeRange = await conn.QueryFirstAsync<TimeRange>($"SELECT MIN(Time) AS StartTime, MAX(Time) AS EndTime FROM ImageEntities INNER JOIN Sessions ON ImageEntities.SessionID = Sessions.ID INNER JOIN Pipelines ON Pipelines.ID = Sessions.PipelineID WHERE Pipelines.Name = '{_PipelineName}'");
            pipelineTimeRange.PipelineID = pipelineID;
            var numNavigationPoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ProcessedNavigations WHERE PipelineID = @PipelineID AND Time > @StartTime AND Time < @EndTime", pipelineTimeRange);
            Dispatch(() => NumNavigationPoints = numNavigationPoints);

            var routeID = await conn.ExecuteScalarAsync<Guid>("SELECT ID FROM Routes WHERE PipelineID = @PipelineID", new { PipelineID = pipelineID });
            var numRoutePoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM RoutePoints WHERE RouteID = @RouteID", new { RouteID = routeID });
            Dispatch(() => NumRoutePoints = numRoutePoints);

            var navigationPoints = await conn.QueryAsync<NavLocation>("SELECT Easting, Northing FROM Navigations WHERE PipelineID = @PipelineID AND Time > @StartTime AND Time < @EndTime", pipelineTimeRange);

            var xs = navigationPoints.Select(p => p.Easting).ToArray();
            var ys = navigationPoints.Select(p => p.Northing).ToArray();
            Dispatch(() => CreatePlotAction(xs, ys, Color.Green));

            var routePoints = await conn.QueryAsync<NavLocation>("SELECT EastingMetres AS Easting, NorthingMetres AS Northing FROM RoutePoints WHERE RouteID = @RouteID", new { RouteID = routeID });
            var routeXs = routePoints.Select(p => p.Easting).ToArray();
            var routeYs = routePoints.Select(p => p.Northing).ToArray();
            Dispatch(() => CreatePlotAction(routeXs, routeYs, Color.Red));
        }
    }

    public ObservableCollection<PipelineStats> PipelineStats
    {
        get => _PipelineStats;
        set
        {
            _PipelineStats = value;
            NotifyPropertyChanged();
        }
    }

    public void Load()
    {
        Task.Run(async () => {
            var pipelines = await GetPipelinesAsync();
            Dispatch(() => PipelineStats = new ObservableCollection<PipelineStats>(pipelines));
        });
    }

    public async Task<List<PipelineStats>> GetPipelinesAsync()
    {
        using (var conn = CreateConnection())
        {
            var pipelines = await conn.QueryAsync<PipelineStats>("SELECT Name, ID FROM Pipelines");

            foreach (var pipeline in pipelines)
            {
                var numNavigationPoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ProcessedNavigations WHERE PipelineID = @PipelineID", new { PipelineID = pipeline.ID });
                var numRawNavigationPoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Navigations WHERE PipelineID = @PipelineID", new { PipelineID = pipeline.ID });
                pipeline.NumProcessedNavigation = numNavigationPoints;
                pipeline.NumRawNavigation = numRawNavigationPoints;
            }

            return pipelines.ToList();
        }
    }

    public void Dispatch(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    public SqlConnection CreateConnection()
    {
        var connectionString = $"Data Source={_ServerName};Initial Catalog={_DatabaseName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";

        var connection = new SqlConnection(connectionString);

        connection.Open();

        return connection;
    }
}

public class TimeRange
{
    public Guid PipelineID { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}

public class NavLocation
{
    public double Easting { get; set; }

    public double Northing { get; set; }
}

public abstract class Model : INotifyPropertyChanged
{
    /// <inheritdoc cref="INotifyPropertyChanged.PropertyChanged"/>
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = "Needs to be compiled with VS2012 or higher")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void NotifyAllPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

public class Command(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        execute();
    }
}

public class PipelineStats
{
    public required string Name { get; set; }

    public required Guid ID { get; set; }

    public int NumRawNavigation { get; set; }

    public int NumProcessedNavigation { get; set; }

    public int Difference => NumProcessedNavigation - NumRawNavigation;
}