using SQLite;

namespace Bannister.Models;

public class ClipPromptTemplate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string PromptBody { get; set; } = "";
    public string ExtraDoNots { get; set; } = "";
}
