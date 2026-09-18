using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class LifePathsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _gameService;
    private readonly DatabaseService _db;
    private readonly LifePathService? _lifePathService;
    private Label _statusLabel = null!;
    private Button _generateBtn = null!;
    private VerticalStackLayout _rowsContainer = null!;
    private Button _btnMonth = null!;
    private Button _btnYear = null!;
    private Button _btnDecade = null!;
    private TimelineData? _data;
    private List<Game> _allGames = new();
    private string _zoom = "year";

    public LifePathsPage(AuthService auth, GameService gameService,
        DatabaseService db, LifePathService? lifePathService = null)
    {
        _auth = auth; _gameService = gameService; _db = db;
        _lifePathService = lifePathService ?? Application.Current?.Handler?.MauiContext?.Services.GetService<LifePathService>();
        Title = "Life Paths"; BackgroundColor = Color.FromArgb("#0D1117"); BuildUI();
    }
    protected override async void OnAppearing()
    { base.OnAppearing(); await LoadDataAsync(); }

    private void BuildUI()
    {
        var page = new VerticalStackLayout { Padding = new Thickness(16,16,16,24), Spacing = 12, BackgroundColor = Color.FromArgb("#0D1117") };
        page.Children.Add(new Label { Text = " Life Paths", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        page.Children.Add(new Label { Text = "Your games over time — activity, gaps, and long-term patterns.", FontSize = 12, TextColor = Color.FromArgb("#8B949E") });
        var zoom = new HorizontalStackLayout { Spacing = 6 };
        _btnMonth = ZoomBtn("Month", false); _btnYear = ZoomBtn("Year", true); _btnDecade = ZoomBtn("Decade", false);
        _btnMonth.Clicked += (_,_) => SetZoom("month"); _btnYear.Clicked += (_,_) => SetZoom("year"); _btnDecade.Clicked += (_,_) => SetZoom("decade");
        zoom.Children.Add(_btnMonth); zoom.Children.Add(_btnYear); zoom.Children.Add(_btnDecade); page.Children.Add(zoom);
        _statusLabel = new Label { Text = "Loading...", FontSize = 12, TextColor = Color.FromArgb("#58A6FF") }; page.Children.Add(_statusLabel);
        _generateBtn = new Button { Text = " Refresh", BackgroundColor = Color.FromArgb("#21262D"), TextColor = Color.FromArgb("#58A6FF"), CornerRadius = 6, HeightRequest = 34, HorizontalOptions = LayoutOptions.Start, Padding = new Thickness(12,0), BorderColor = Color.FromArgb("#30363D"), BorderWidth = 1 };
        _generateBtn.Clicked += async (_,_) => await LoadDataAsync(); page.Children.Add(_generateBtn);
        var legend = new HorizontalStackLayout { Spacing = 16 }; legend.Children.Add(Legend(Color.FromArgb("#1F6FEB"),"Active")); legend.Children.Add(Legend(Color.FromArgb("#21262D"),"Gap")); legend.Children.Add(Legend(Color.FromArgb("#F85149"),"Ended")); page.Children.Add(legend);
        _rowsContainer = new VerticalStackLayout { Spacing = 2 }; page.Children.Add(_rowsContainer); Content = new ScrollView { Content = page, BackgroundColor = Color.FromArgb("#0D1117") };
    }
    private static Button ZoomBtn(string text, bool active) => new() { Text=text, FontSize=11, HeightRequest=28, CornerRadius=4, Padding=new Thickness(10,0), BackgroundColor=active?Color.FromArgb("#1F6FEB"):Color.FromArgb("#21262D"), TextColor=active?Colors.White:Color.FromArgb("#8B949E"), BorderColor=Color.FromArgb("#30363D"), BorderWidth=1 };
    private static View Legend(Color color,string text) { var r=new HorizontalStackLayout{Spacing=4}; r.Children.Add(new BoxView{Color=color,WidthRequest=14,HeightRequest=8,CornerRadius=2}); r.Children.Add(new Label{Text=text,FontSize=10,TextColor=Color.FromArgb("#8B949E")}); return r; }
    private void SetZoom(string zoom) { _zoom=zoom; Style(_btnMonth,zoom=="month"); Style(_btnYear,zoom=="year"); Style(_btnDecade,zoom=="decade"); if(_data!=null) RenderRows(); }
    private static void Style(Button b,bool active) { b.BackgroundColor=active?Color.FromArgb("#1F6FEB"):Color.FromArgb("#21262D"); b.TextColor=active?Colors.White:Color.FromArgb("#8B949E"); }

    private async Task LoadDataAsync()
    {
        _statusLabel.Text="Loading game history..."; _generateBtn.IsEnabled=false; _rowsContainer.Children.Clear();
        try { var (data,games)=await BuildTimelineDataAsync(); _data=data; _allGames=games; _statusLabel.Text=$"✓ {data.Games.Count} games · {data.TotalLogs:N0} records"; RenderRows(); }
        catch(Exception ex) { _statusLabel.Text=$"Error: {ex.Message}"; } finally { _generateBtn.IsEnabled=true; }
    }
    private void RenderRows()
    {
        _rowsContainer.Children.Clear(); if(_data==null)return; var games=_data.Games.OrderBy(g=>g.StartDate).ToList(); if(games.Count==0)return;
        var min=games.Min(g=>g.StartDate); var max=games.Max(g=>g.EndDate); if(max<DateTime.Now)max=DateTime.Now; DateTime start,end;
        if(_zoom=="decade"){start=new DateTime(min.Year/10*10,1,1);end=new DateTime(max.Year/10*10+10,1,1);} else if(_zoom=="month"){start=new DateTime(min.Year,min.Month,1);end=new DateTime(max.Year,max.Month,1).AddMonths(1);} else {start=new DateTime(min.Year,1,1);end=new DateTime(max.Year+1,1,1);}
        double span=(end-start).TotalDays; _rowsContainer.Children.Add(Axis(start,end,span)); foreach(var g in games)_rowsContainer.Children.Add(GameRow(g,start,span));
    }
    private View Axis(DateTime start,DateTime end,double span)
    {
        const double lw=130,bw=220; var grid=new Grid{ColumnDefinitions={new ColumnDefinition(new GridLength(lw)),new ColumnDefinition(new GridLength(bw))}}; var ticks=new Grid();
        foreach(var t in Ticks(start,end)){double x=(t.Date-start).TotalDays/span*bw; ticks.Children.Add(new Label{Text=t.Label,FontSize=9,TextColor=Color.FromArgb("#8B949E"),TranslationX=x});}
        grid.Add(new Label{Text="Game",FontSize=10,TextColor=Color.FromArgb("#8B949E")},0,0);grid.Add(ticks,1,0);return grid;
    }
    private View GameRow(GameTimelineRow game,DateTime start,double span)
    {
        const double lw=130,bw=220,bh=14; double rh=game.SubBlocks.Count>0?36:20; var grid=new Grid{ColumnDefinitions={new ColumnDefinition(new GridLength(lw)),new ColumnDefinition(new GridLength(bw)),new ColumnDefinition(new GridLength(30))},ColumnSpacing=4};
        grid.Add(new Label{Text=game.DisplayName,FontSize=11,TextColor=game.IsActive?Colors.White:Color.FromArgb("#8B949E"),LineBreakMode=LineBreakMode.TailTruncation},0,0);
        var canvas=new GraphicsView{HeightRequest=rh,WidthRequest=bw,Drawable=new BarDrawable(game,start,span,bw,rh,bh)};var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await ShowDetail(game);canvas.GestureRecognizers.Add(tap);grid.Add(canvas,1,0);
        if(_lifePathService!=null){var edit=new Button{Text="✏",HeightRequest=24,WidthRequest=28,Padding=0,BackgroundColor=Color.FromArgb("#21262D"),TextColor=Color.FromArgb("#58A6FF")};var captured=_allGames.FirstOrDefault(g=>g.GameId==game.GameId);edit.Clicked+=async(_,_)=>{if(captured==null)return;await Navigation.PushAsync(new LifePathEditorPage(captured,_lifePathService,_gameService,_auth));await LoadDataAsync();};grid.Add(edit,2,0);}return grid;
    }
    private async Task ShowDetail(GameTimelineRow g){await DisplayAlert(g.DisplayName,$"Started: {g.StartDate:dd MMM yyyy}\nLast active: {g.EndDate:dd MMM yyyy}\nRecords: {g.TotalLogs:N0}\nFocus blocks: {g.SubBlocks.Count}","OK");}
    private List<(DateTime Date,string Label)> Ticks(DateTime s,DateTime e){var r=new List<(DateTime,string)>();if(_zoom=="month")for(var d=new DateTime(s.Year,s.Month,1);d<=e;d=d.AddMonths(1))r.Add((d,d.ToString("MMM yy")));else if(_zoom=="decade")for(int y=s.Year/10*10;y<=e.Year+10;y+=10)r.Add((new DateTime(y,1,1),y.ToString()));else for(int y=s.Year;y<=e.Year+1;y++)r.Add((new DateTime(y,1,1),y.ToString()));return r;}

    private async Task<(TimelineData Data,List<Game> Games)> BuildTimelineDataAsync()
    {
        var username=_auth.CurrentUsername;var conn=await _db.GetConnectionAsync();var allGames=await conn.Table<Game>().Where(g=>g.Username==username).ToListAsync();var allLogs=await conn.Table<ExpLog>().Where(e=>e.Username==username).ToListAsync();var byGame=allLogs.GroupBy(e=>e.Game).ToDictionary(g=>g.Key,g=>g.OrderBy(e=>e.LoggedAt).ToList());var rows=new List<GameTimelineRow>();
        foreach(var game in allGames.OrderBy(g=>g.CreatedAt)){var logs=byGame.TryGetValue(game.GameId,out var found)?found:new();var start=game.CreatedAt.ToLocalTime();DateTime? last=logs.Count>0?logs.Last().LoggedAt.ToLocalTime():null;var end=last??game.LastVisitedAt?.ToLocalTime()??start;var blocks=new List<SubBlock>();if(_lifePathService!=null){var bs=await _lifePathService.GetBlocksForGameAsync(username,game.GameId);blocks=bs.Select(b=>new SubBlock{Id=b.Id,ParentId=b.ParentBlockId,Label=b.Label,Reason=b.Reason,StartDate=b.StartDate,EndDate=b.EndDate,Level=b.Level,ColorHex=b.ColorHex}).ToList();}rows.Add(new GameTimelineRow{GameId=game.GameId,DisplayName=game.DisplayName,StartDate=start,EndDate=game.LifePathEndedAt?.ToLocalTime()??end,EndedAt=game.LifePathEndedAt?.ToLocalTime(),EndReason=game.LifePathEndReason??"",IsActive=game.IsActive,TotalLogs=logs.Count,Periods=DetectPeriods(logs,start),SubBlocks=blocks});}
        return (new TimelineData{Username=username,GeneratedAt=DateTime.Now,Games=rows,TotalLogs=allLogs.Count},allGames);
    }
    private static List<ActivePeriod> DetectPeriods(List<ExpLog> logs,DateTime start){var r=new List<ActivePeriod>();if(logs.Count==0){r.Add(new ActivePeriod{Start=start,End=start,IsGap=true});return r;}var s=logs.OrderBy(l=>l.LoggedAt).ToList();var ps=s[0].LoggedAt.ToLocalTime();var pe=ps;int n=1;for(int i=1;i<s.Count;i++){var c=s[i].LoggedAt.ToLocalTime();if((c-pe).TotalDays>30){r.Add(new ActivePeriod{Start=ps,End=pe,LogCount=n});r.Add(new ActivePeriod{Start=pe,End=c,IsGap=true});ps=c;pe=c;n=1;}else{pe=c;n++;}}r.Add(new ActivePeriod{Start=ps,End=pe,LogCount=n});return r;}

    private class TimelineData{public string Username{get;set;}="";public DateTime GeneratedAt{get;set;}public List<GameTimelineRow> Games{get;set;}=new();public int TotalLogs{get;set;}}
    private class GameTimelineRow{public string GameId{get;set;}="";public string DisplayName{get;set;}="";public DateTime StartDate{get;set;}public DateTime EndDate{get;set;}public DateTime? EndedAt{get;set;}public string EndReason{get;set;}="";public bool IsActive{get;set;}public int TotalLogs{get;set;}public List<ActivePeriod> Periods{get;set;}=new();public List<SubBlock> SubBlocks{get;set;}=new();}
    private class SubBlock{public int Id{get;set;}public int? ParentId{get;set;}public string Label{get;set;}="";public string Reason{get;set;}="";public DateTime StartDate{get;set;}public DateTime? EndDate{get;set;}public int Level{get;set;}public string ColorHex{get;set;}="";}
    private class ActivePeriod{public DateTime Start{get;set;}public DateTime End{get;set;}public bool IsGap{get;set;}public int LogCount{get;set;}}
    private sealed class BarDrawable:IDrawable
    {
        private readonly GameTimelineRow _game;private readonly DateTime _start;private readonly double _span,_width,_height,_bar;
        public BarDrawable(GameTimelineRow game,DateTime start,double span,double width,double height,double bar){_game=game;_start=start;_span=span;_width=width;_height=height;_bar=bar;}
        private float X(DateTime d)=>(float)((d-_start).TotalDays/_span*_width);
        public void Draw(ICanvas c,RectF r){c.FillColor=Color.FromArgb("#21262D");c.FillRoundedRectangle(0,0,(float)_width,(float)_bar,3);foreach(var p in _game.Periods){var x=Math.Max(0,X(p.Start));var x2=Math.Max(x+2,X(p.End));c.FillColor=p.IsGap?Color.FromArgb("#21262D"):Color.FromArgb("#1F6FEB");c.FillRoundedRectangle(x,0,Math.Min((float)_width-x,x2-x),(float)_bar,3);}foreach(var b in _game.SubBlocks){var x=Math.Max(0,X(b.StartDate));var x2=b.EndDate.HasValue?X(b.EndDate.Value):X(DateTime.Now);c.FillColor=Color.FromArgb(string.IsNullOrWhiteSpace(b.ColorHex)?"#388BFD":b.ColorHex);c.FillRoundedRectangle(x,b.Level==2?(float)_bar+12:(float)_bar+2,Math.Max(2,x2-x),b.Level==2?5:8,2);}if(_game.EndedAt.HasValue){c.StrokeColor=Color.FromArgb("#F85149");c.StrokeSize=2;var x=X(_game.EndedAt.Value);c.DrawLine(x,0,x,(float)_height);}}
    }
}
