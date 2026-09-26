using SQLite;

namespace Bannister.Models;

[Table("sequence_task_groups")]
public class SequenceTaskGroup
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string ExportPromptTemplate { get; set; } = "{tasks}";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool IsArchived { get; set; }
}

[Table("sequence_task_items")]
public class SequenceTaskItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int TaskItemId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPendingTest { get; set; }
}

[Table("sequence_task_exceptions")]
public class SequenceTaskException
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int GroupId { get; set; }
    public string Label { get; set; } = "";
}
