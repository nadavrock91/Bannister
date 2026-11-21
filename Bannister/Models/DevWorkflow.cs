using SQLite;

namespace Bannister.Models;

[Table("dev_workflows")]
public class DevWorkflow
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

[Table("dev_workflow_steps")]
public class DevWorkflowStep
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    [Indexed] public int WorkflowId { get; set; }
    public int SortOrder { get; set; }
    public string StepType { get; set; } = "Manual";
    public string Text { get; set; } = "";
    public bool UseAllConstraints { get; set; }
    public string CheckKind { get; set; } = "";
    public string WrapPrefix { get; set; } = "";
    public string WrapSuffix { get; set; } = "";
    public bool AppendConstraints { get; set; }
}

[Table("dev_programming_constraints")]
public class DevProgrammingConstraint
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
}

[Table("dev_workflow_overrides")]
public class DevWorkflowOverride
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public int WorkflowId { get; set; }
    public int StepId { get; set; }
    public string WorkflowName { get; set; } = "";
    public string StepName { get; set; } = "";
    public DateTime OverriddenAt { get; set; } = DateTime.UtcNow;
}

[Table("dev_workflow_device_settings")]
public class DevWorkflowDeviceSetting
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string MachineName { get; set; } = "";
    public string RepoFolder { get; set; } = "";
    public int? LastUsedWorkflowId { get; set; }
    public int? LastUsedProjectId { get; set; }
    public bool LegacyRepoMigrated { get; set; }
}

[Table("dev_workflow_projects")]
public class DevWorkflowProject
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

[Table("dev_workflow_project_folders")]
public class DevWorkflowProjectFolder
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int ProjectId { get; set; }
    [Indexed] public string MachineName { get; set; } = "";
    public string FolderPath { get; set; } = "";
}

public sealed class DevGitCheckResult
{
    public bool IsAvailable { get; init; }
    public bool Passed { get; init; }
    public bool IsBehind { get; init; }
    public string Message { get; init; } = "";
}
