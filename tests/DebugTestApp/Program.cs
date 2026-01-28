// Test app for debugger attach testing
var app = new Application("TestApp", 42);
app.Run();

public class Address
{
    public string Street { get; set; } = "Main Street";
    public int Number { get; set; } = 123;
    public string City { get; set; } = "Warsaw";
}

public class Person
{
    public string Name { get; set; } = "John";
    public int Age { get; set; } = 30;
    public Address HomeAddress { get; set; } = new Address();
    public List<string> Tags { get; set; } = new() { "developer", "tester" };
}

public class Application
{
    private readonly string _name;
    private readonly int _id;
    private Person _currentUser = new Person();
    private Dictionary<string, object> _settings = new()
    {
        ["debug"] = true,
        ["timeout"] = 5000,
        ["name"] = "DebugTest"
    };

    public Application(string name, int id)
    {
        _name = name;
        _id = id;
    }

    public void Run()
    {
        Console.WriteLine($"Application '{_name}' (ID: {_id}) started. PID: {Environment.ProcessId}");
        Console.WriteLine("READY");
        Console.Out.Flush();

        int counter = 0;
        while (true)
        {
            ProcessIteration(counter);
            counter++;
            Thread.Sleep(1000);
        }
    }

    private void ProcessIteration(int iteration)
    {
        var localData = new { Iteration = iteration, Timestamp = DateTime.Now };
        var message = $"Processing iteration {iteration}";

        DoWork(message, localData.Timestamp);
    }

    private void DoWork(string message, DateTime timestamp)
    {
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Description = message,
            CreatedAt = timestamp,
            Priority = (int)(timestamp.Ticks % 5)
        };

        ExecuteWorkItem(workItem);
    }

    private void ExecuteWorkItem(WorkItem item)
    {
        // This is where we spend most time - good place to pause
        var result = item.Priority * 10;
        var status = result > 20 ? "High" : "Normal";

        // Simulate some work
        Thread.Sleep(100);
    }
}

public class WorkItem
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int Priority { get; set; }
}
