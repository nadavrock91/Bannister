using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class JournalAnalysisPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly JournalAnalysisService _analysis;
    private readonly IJournalAnalysisProvider _provider;
    private readonly JournalService _journalService;
    private readonly DatabaseService _db;
    private Label _statusLabel = null!;
    private Label _checkpointLabel = null!;
    private VerticalStackLayout _proposalsContainer = null!;
    private Button _runBtn = null!;
    private Button _runFullBtn = null!;
    private bool _showResolved;
    private bool _isRunning;

    public JournalAnalysisPage(AuthService auth, JournalAnalysisService analysis,
        IJournalAnalysisProvider provider, JournalService journalService, DatabaseService db)
    { _auth=auth; _analysis=analysis; _provider=provider; _journalService=journalService; _db=db; Title="Journal Analysis"; BackgroundColor=Color.FromArgb("#F5F5F5"); BuildUI(); }
    protected override async void OnAppearing(){base.OnAppearing();await RefreshCheckpointAsync();await LoadProposalsAsync();}
    private void BuildUI()
    {
        var stack=new VerticalStackLayout{Padding=20,Spacing=12};
        stack.Children.Add(new Label{Text=" Journal Analysis",FontSize=22,FontAttributes=FontAttributes.Bold,TextColor=Color.FromArgb("#222")});
        stack.Children.Add(new Label{Text="Analyze journal entries to propose evidence-backed Life Path updates. Every proposal requires approval.",FontSize=13,TextColor=Color.FromArgb("#666")});
        stack.Children.Add(new Label{Text=$"Provider: {_provider.ProviderName}",FontSize=11,TextColor=Color.FromArgb("#999")});
        _checkpointLabel=new Label{FontSize=12,TextColor=Color.FromArgb("#666")};stack.Children.Add(_checkpointLabel);
        var buttons=new HorizontalStackLayout{Spacing=8};
        _runBtn=new Button{Text="▶ Analyze New Entries",BackgroundColor=Color.FromArgb("#1565C0"),TextColor=Colors.White,CornerRadius=8,HeightRequest=40};_runBtn.Clicked+=async(_,_)=>await RunAsync(false);buttons.Children.Add(_runBtn);
        _runFullBtn=new Button{Text="↺ Full Re-analysis",BackgroundColor=Color.FromArgb("#ECEFF1"),TextColor=Color.FromArgb("#37474F"),CornerRadius=8,HeightRequest=40};_runFullBtn.Clicked+=async(_,_)=>await RunAsync(true);buttons.Children.Add(_runFullBtn);stack.Children.Add(buttons);
        _statusLabel=new Label{FontSize=12,TextColor=Color.FromArgb("#1565C0")};stack.Children.Add(_statusLabel);
        var resolved=new Switch();resolved.Toggled+=async(_,e)=>{_showResolved=e.Value;await LoadProposalsAsync();};var resolvedRow=new HorizontalStackLayout{Spacing=8};resolvedRow.Children.Add(new Label{Text="Show resolved proposals",TextColor=Color.FromArgb("#666")});resolvedRow.Children.Add(resolved);stack.Children.Add(resolvedRow);
        _proposalsContainer=new VerticalStackLayout{Spacing=10};stack.Children.Add(_proposalsContainer);Content=new ScrollView{Content=stack};
    }
    private async Task RefreshCheckpointAsync(){var cp=await _analysis.GetCheckpointAsync(_auth.CurrentUsername);_checkpointLabel.Text=cp.LastAnalyzedAt.HasValue?$"Last analysis: {cp.LastRunAt?.ToLocalTime():dd MMM yyyy HH:mm} · Run #{cp.TotalRunCount}":"No previous analysis — first run will process all entries.";}
    private async Task LoadProposalsAsync()
    {
        _proposalsContainer.Children.Clear();List<LifePathProposal> proposals;
        if(_showResolved){var conn=await _db.GetConnectionAsync();proposals=await conn.Table<LifePathProposal>().Where(p=>p.Username==_auth.CurrentUsername).ToListAsync();}else proposals=await _analysis.GetPendingProposalsAsync(_auth.CurrentUsername);
        foreach(var p in proposals.OrderByDescending(p=>p.CreatedAt))_proposalsContainer.Children.Add(Card(p));
        if(proposals.Count==0)_proposalsContainer.Children.Add(new Label{Text="No proposals yet. Run an analysis to generate proposals.",TextColor=Color.FromArgb("#999")});
    }
    private View Card(LifePathProposal p)
    {
        var inner=new VerticalStackLayout{Spacing=5};inner.Children.Add(new Label{Text=$"{(ProposalType)p.ProposalType} · {p.Status}",FontAttributes=FontAttributes.Bold,TextColor=Color.FromArgb("#3949AB")});inner.Children.Add(new Label{Text=$"Game: {p.GameId}\n{p.ProposedLabel}",TextColor=Color.FromArgb("#222")});if(!string.IsNullOrWhiteSpace(p.ProposedReason))inner.Children.Add(new Label{Text=p.ProposedReason,TextColor=Color.FromArgb("#555")});if(!string.IsNullOrWhiteSpace(p.Evidence))inner.Children.Add(new Label{Text=$"Evidence: {p.Evidence}",TextColor=Color.FromArgb("#666")});
        if((ProposalStatus)p.Status==ProposalStatus.Pending){var row=new HorizontalStackLayout{Spacing=6};var accept=new Button{Text="✓ Accept",BackgroundColor=Color.FromArgb("#2E7D32"),TextColor=Colors.White,HeightRequest=34};accept.Clicked+=async(_,_)=>{await _analysis.AcceptProposalAsync(p,_auth.CurrentUsername);await LoadProposalsAsync();};var reject=new Button{Text="✕ Reject",BackgroundColor=Color.FromArgb("#FFEBEE"),TextColor=Color.FromArgb("#C62828"),HeightRequest=34};reject.Clicked+=async(_,_)=>{await _analysis.RejectProposalAsync(p);await LoadProposalsAsync();};row.Children.Add(accept);row.Children.Add(reject);inner.Children.Add(row);}
        return new Frame{Content=inner,Padding=12,CornerRadius=8,HasShadow=false,BorderColor=Color.FromArgb("#E0E0E0"),BackgroundColor=Colors.White};
    }
    private async Task RunAsync(bool full)
    {
        if(_isRunning)return; if(full&&!await DisplayAlert("Full Re-analysis","Analyze all journal entries again?","Continue","Cancel"))return;_isRunning=true;_runBtn.IsEnabled=false;_runFullBtn.IsEnabled=false;_statusLabel.Text="Running analysis...";
        try{var (_,proposals)=await _analysis.RunAnalysisAsync(_auth.CurrentUsername,_provider,fullReanalysis:full);_statusLabel.Text=$"✓ {proposals.Count} proposal(s) generated.";await RefreshCheckpointAsync();await LoadProposalsAsync();}catch(Exception ex){_statusLabel.Text=$"Error: {ex.Message}";}finally{_isRunning=false;_runBtn.IsEnabled=true;_runFullBtn.IsEnabled=true;}
    }
}
