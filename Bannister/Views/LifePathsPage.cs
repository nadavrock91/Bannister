using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Graphics;

namespace Bannister.Views;

public record PeriodData(DateTime Start, DateTime End, bool IsGap, int LogCount);
public record SubBlockData(int Id, int? ParentId, string Label, DateTime StartDate, DateTime? EndDate, int Level, string ColorHex);

public class TimelineBarDrawable : IDrawable
{
    private readonly DateTime _rangeStart; private readonly double _span;
    private readonly float _barW, _barH, _rowH; private readonly Color _base;
    private readonly IReadOnlyList<PeriodData> _periods;
    private readonly IReadOnlyList<SubBlockData> _blocks;
    private readonly DateTime? _endedAt; private readonly bool _showSubBlocks;
    public TimelineBarDrawable(DateTime start, double span, float width, float rowH, float barH, Color color, IReadOnlyList<PeriodData> periods, IReadOnlyList<SubBlockData> blocks, DateTime? endedAt, bool isActive, bool showSubBlocks=true)
    { _rangeStart=start; _span=span; _barW=width; _rowH=rowH; _barH=barH; _base=color; _periods=periods; _blocks=blocks; _endedAt=endedAt; _showSubBlocks=showSubBlocks; }
    private float X(DateTime d)=>(float)((d-_rangeStart).TotalDays/_span*_barW);
    public void Draw(ICanvas canvas, RectF dirty)
    {
        canvas.FillColor=Color.FromArgb("#1C2128"); canvas.FillRoundedRectangle(0,0,_barW,_barH,3);
        foreach(var p in _periods.Where(p=>!p.IsGap)){float x0=Math.Max(0,X(p.Start)),x1=Math.Min(_barW,X(p.End));if(x1<=x0)continue;canvas.FillColor=_base.WithAlpha(Math.Min(1f,.35f+p.LogCount/300f));canvas.FillRoundedRectangle(x0,0,x1-x0,_barH,2);}
        if(_showSubBlocks){float y=_barH+3; foreach(var s in _blocks.Where(b=>b.Level==1&&b.ParentId==null)){float x0=Math.Max(0,X(s.StartDate)),x1=Math.Min(_barW,X(s.EndDate??DateTime.Now));if(x1<=x0)continue;var color=string.IsNullOrWhiteSpace(s.ColorHex)?_base.WithAlpha(.85f):Color.FromArgb(s.ColorHex);canvas.FillColor=color;canvas.FillRoundedRectangle(x0,y,x1-x0,7,2);if(x1-x0>40){canvas.FontColor=Colors.White.WithAlpha(.9f);canvas.FontSize=8;canvas.DrawString(s.Label.Length>24?s.Label[..21]+"…":s.Label,x0+3,y,x1-x0-4,7,HorizontalAlignment.Left,VerticalAlignment.Center);}foreach(var c in _blocks.Where(b=>b.Level==2&&b.ParentId==s.Id)){float cx0=Math.Max(0,X(c.StartDate)),cx1=Math.Min(_barW,X(c.EndDate??DateTime.Now));if(cx1<=cx0)continue;canvas.FillColor=color.WithAlpha(.55f);canvas.FillRoundedRectangle(cx0,y+9,cx1-cx0,5,2);}}}
        float today=X(DateTime.Now);if(today>0&&today<_barW){canvas.StrokeColor=Color.FromArgb("#3FB950");canvas.StrokeSize=1.5f;canvas.DrawLine(today,0,today,_barH);}if(_endedAt.HasValue){float end=X(_endedAt.Value);if(end>0&&end<_barW){canvas.StrokeColor=Color.FromArgb("#F85149");canvas.StrokeSize=2;canvas.DrawLine(end,0,end,_barH);}}
    }
}

public class LifePathsPage : ContentPage
{
    private readonly AuthService _auth; private readonly GameService _gameService; private readonly DatabaseService _db; private readonly LifePathService? _lifePathService;
    private Label _statusLabel=null!; private Button _generateBtn=null!; private VerticalStackLayout _rowsContainer=null!; private Grid _axisContainer=null!; private Button _btnMonth=null!; private Button _btnYear=null!; private Button _btnDecade=null!;
    private List<GameTimelineRow> _gameRows=new(); private List<Game> _allGames=new(); private string _zoom="year"; private bool _isExpanded=false; private Button _expandBtn=null!;
    private HashSet<string> _pinnedGameIds = new();
    private const string PinnedKeyPrefix = "lifepaths_pinned_";
    private const float LabelW=110f, EditW=28f, Gutter=8f, BarH=16f;
    public LifePathsPage(AuthService auth, GameService gameService, DatabaseService db, LifePathService? lifePathService=null){_auth=auth;_gameService=gameService;_db=db;_lifePathService=lifePathService;Title="Life Paths";BackgroundColor=Color.FromArgb("#0D1117");BuildUI();}
    protected override async void OnAppearing(){base.OnAppearing();LoadPinnedGames();await LoadDataAsync();}
    private float GetBarW()
    {
        double screenW=DeviceDisplay.MainDisplayInfo.Width/DeviceDisplay.MainDisplayInfo.Density;
        float available=(float)screenW-LabelW-EditW*2-Gutter*4-32;
        return Math.Max(200f,available);
    }
    private void LoadPinnedGames()
    {
        string key = PinnedKeyPrefix + _auth.CurrentUsername;
        string json = Preferences.Default.Get(key, "[]");
        try
        {
            var ids = System.Text.Json.JsonSerializer
                .Deserialize<List<string>>(json) ?? new();
            _pinnedGameIds = new HashSet<string>(ids);
        }
        catch { _pinnedGameIds = new(); }
    }
    private void SavePinnedGames()
    {
        string key = PinnedKeyPrefix + _auth.CurrentUsername;
        string json = System.Text.Json.JsonSerializer
            .Serialize(_pinnedGameIds.ToList());
        Preferences.Default.Set(key, json);
    }
    private void TogglePin(string gameId)
    {
        if (_pinnedGameIds.Contains(gameId))
            _pinnedGameIds.Remove(gameId);
        else
            _pinnedGameIds.Add(gameId);
        SavePinnedGames();
        RenderRows();
    }
    private void BuildUI()
    {
        var page=new VerticalStackLayout{Padding=new Thickness(16,16,16,32),Spacing=10,BackgroundColor=Color.FromArgb("#0D1117")};page.Children.Add(new Label{Text=" Life Paths",FontSize=22,FontAttributes=FontAttributes.Bold,TextColor=Colors.White});page.Children.Add(new Label{Text="Your games over time. Tap a bar for details. Tap ✏ to edit blocks.",FontSize=12,TextColor=Color.FromArgb("#8B949E")});
        var controls=new HorizontalStackLayout{Spacing=6};_btnMonth=Zoom("Month",false);_btnYear=Zoom("Year",true);_btnDecade=Zoom("Decade",false);_btnMonth.Clicked+=(_,_)=>SetZoom("month");_btnYear.Clicked+=(_,_)=>SetZoom("year");_btnDecade.Clicked+=(_,_)=>SetZoom("decade");controls.Children.Add(_btnMonth);controls.Children.Add(_btnYear);controls.Children.Add(_btnDecade);_generateBtn=new Button{Text="",HeightRequest=28,WidthRequest=36,Padding=0,BackgroundColor=Color.FromArgb("#21262D"),TextColor=Color.FromArgb("#58A6FF")};_generateBtn.Clicked+=async(_,_)=>await LoadDataAsync();controls.Children.Add(_generateBtn);_expandBtn=new Button{Text="⊞ Expand",FontSize=11,HeightRequest=28,CornerRadius=4,Padding=new Thickness(10,0),BackgroundColor=Color.FromArgb("#21262D"),TextColor=Color.FromArgb("#8B949E"),BorderColor=Color.FromArgb("#30363D"),BorderWidth=1};_expandBtn.Clicked+=(_,_)=>{_isExpanded=!_isExpanded;_expandBtn.Text=_isExpanded?"⊟ Collapse":_pinnedGameIds.Count>0?$"⊞ All ({_gameRows.Count})":"⊞ Expand";_expandBtn.TextColor=_isExpanded?Colors.White:Color.FromArgb("#8B949E");RenderRows();};controls.Children.Add(_expandBtn);page.Children.Add(controls);
        var legend=new HorizontalStackLayout{Spacing=12};legend.Children.Add(Legend("#388BFD","Active"));legend.Children.Add(Legend("#1C2128","Gap"));legend.Children.Add(Legend("#3FB950","Today"));legend.Children.Add(Legend("#F85149","Ended"));page.Children.Add(legend);_statusLabel=new Label{FontSize=11,TextColor=Color.FromArgb("#8B949E")};page.Children.Add(_statusLabel);_axisContainer=new Grid();page.Children.Add(_axisContainer);_rowsContainer=new VerticalStackLayout{Spacing=2};page.Children.Add(_rowsContainer);Content=new ScrollView{Content=page,BackgroundColor=Color.FromArgb("#0D1117")};
    }
    private static Button Zoom(string text,bool active)=>new(){Text=text,FontSize=11,HeightRequest=28,CornerRadius=4,Padding=new Thickness(10,0),BackgroundColor=active?Color.FromArgb("#1F6FEB"):Color.FromArgb("#21262D"),TextColor=active?Colors.White:Color.FromArgb("#8B949E")};
    private static View Legend(string hex,string text){var row=new HorizontalStackLayout{Spacing=4};row.Children.Add(new BoxView{Color=Color.FromArgb(hex),WidthRequest=12,HeightRequest=8});row.Children.Add(new Label{Text=text,FontSize=10,TextColor=Color.FromArgb("#8B949E")});return row;}
    private void SetZoom(string z){_zoom=z;Style(_btnMonth,z=="month");Style(_btnYear,z=="year");Style(_btnDecade,z=="decade");RenderRows();}
    private static void Style(Button b,bool active){b.BackgroundColor=active?Color.FromArgb("#1F6FEB"):Color.FromArgb("#21262D");b.TextColor=active?Colors.White:Color.FromArgb("#8B949E");}
    private async Task LoadDataAsync(){_statusLabel.Text="Loading...";_generateBtn.IsEnabled=false;try{var(rows,games)=await BuildTimelineDataAsync();_gameRows=rows;_allGames=games;_statusLabel.Text=$"{rows.Count} games · {rows.Sum(r=>r.TotalLogs):N0} records"+(_pinnedGameIds.Count>0?$" · {_pinnedGameIds.Count} pinned":" · tap · to pin games");RenderRows();}catch(Exception ex){_statusLabel.Text=$"Error: {ex.Message}";}finally{_generateBtn.IsEnabled=true;}}
    private (DateTime Start,DateTime End,double Span) Range(){if(_gameRows.Count==0)return(DateTime.Today,DateTime.Today.AddYears(1),365);var min=_gameRows.Min(g=>g.StartDate);var max=_gameRows.Max(g=>g.EndDate);if(max<DateTime.Now)max=DateTime.Now;DateTime s,e;if(_zoom=="decade"){s=new DateTime(min.Year/10*10,1,1);e=new DateTime(max.Year/10*10+10,1,1);}else if(_zoom=="month"){s=new DateTime(min.Year,min.Month,1);e=new DateTime(max.Year,max.Month,1).AddMonths(1);}else{s=new DateTime(min.Year,1,1);e=new DateTime(max.Year+1,1,1);}return(s,e,Math.Max(1,(e-s).TotalDays));}
    private void RenderRows(){_rowsContainer.Children.Clear();_axisContainer.Children.Clear();if(_gameRows.Count==0)return;var(rStart,rEnd,span)=Range();float barW=GetBarW();_axisContainer.Children.Add(BuildAxis(rStart,rEnd,span,barW));var sortedGames=_gameRows.OrderBy(g=>g.DisplayName,StringComparer.OrdinalIgnoreCase).ToList();List<GameTimelineRow> visibleGames;if(_isExpanded){visibleGames=sortedGames;}else if(_pinnedGameIds.Count>0){visibleGames=sortedGames.Where(g=>_pinnedGameIds.Contains(g.GameId)).ToList();}else{_rowsContainer.Children.Add(new Label{Text="No games pinned yet.\nTap ⊞ All to see all games, then tap · next to each game you want to monitor here.",FontSize=13,TextColor=Color.FromArgb("#8B949E"),LineBreakMode=LineBreakMode.WordWrap,Margin=new Thickness(0,8)});return;}foreach(var g in visibleGames)_rowsContainer.Children.Add(BuildGameRow(g,rStart,span,barW));}
    private View BuildAxis(DateTime start,DateTime end,double span,float barW){float edit=_lifePathService!=null?EditW+Gutter:0;var grid=new Grid{ColumnDefinitions={new ColumnDefinition(new GridLength(LabelW)),new ColumnDefinition(new GridLength(barW)),new ColumnDefinition(new GridLength(edit)),new ColumnDefinition(new GridLength(EditW))}};grid.Add(new Label{Text=""},0,0);grid.Add(new GraphicsView{Drawable=new AxisDrawable(start,end,span,barW),HeightRequest=20,WidthRequest=barW},1,0);return grid;}
    private View BuildGameRow(GameTimelineRow game,DateTime start,double span,float barW)
    {
        bool level2=_isExpanded&&game.SubBlocks.Any(b=>b.Level==2);float rowH=level2?42:_isExpanded&&game.SubBlocks.Count>0?30:20;int hue=Math.Abs(game.GameId.GetHashCode())%360;var color=Color.FromHsv(hue/360f,.65f,.78f);var drawable=new TimelineBarDrawable(start,span,barW,rowH,BarH,color,game.Periods.Select(p=>new PeriodData(p.Start,p.End,p.IsGap,p.LogCount)).ToList(),game.SubBlocks,game.EndedAt,game.IsActive,_isExpanded);var canvas=new GraphicsView{Drawable=drawable,HeightRequest=rowH,WidthRequest=barW};var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>{var capturedGame2=_allGames.FirstOrDefault(g=>g.GameId==game.GameId);if(capturedGame2==null||_lifePathService==null){await ShowDetailAsync(game);return;}await Navigation.PushAsync(new LifePathEditorPage(capturedGame2,_lifePathService,_gameService,_auth));await LoadDataAsync();};canvas.GestureRecognizers.Add(tap);float edit=_lifePathService!=null?EditW+Gutter:0;var row=new Grid{ColumnDefinitions={new ColumnDefinition(new GridLength(LabelW)),new ColumnDefinition(new GridLength(barW)),new ColumnDefinition(new GridLength(edit)),new ColumnDefinition(new GridLength(EditW))},ColumnSpacing=Gutter};row.Add(new Label{Text=game.DisplayName,FontSize=10,TextColor=game.IsActive?Color.FromArgb("#C9D1D9"):Color.FromArgb("#484F58"),LineBreakMode=LineBreakMode.TailTruncation,MaxLines=2},0,0);row.Add(canvas,1,0);if(_lifePathService!=null){var btn=new Button{Text="✏",HeightRequest=20,WidthRequest=EditW,Padding=0,BackgroundColor=Color.FromArgb("#21262D"),TextColor=Color.FromArgb("#8B949E")};var captured=_allGames.FirstOrDefault(g=>g.GameId==game.GameId);btn.Clicked+=async(_,_)=>{if(captured==null)return;await Navigation.PushAsync(new LifePathEditorPage(captured,_lifePathService!,_gameService,_auth));await LoadDataAsync();};row.Add(btn,2,0);}bool isPinned=_pinnedGameIds.Contains(game.GameId);var pinBtn=new Button{Text=isPinned?"":"·",BackgroundColor=isPinned?Color.FromArgb("#2D3A2E"):Color.FromArgb("#21262D"),TextColor=isPinned?Color.FromArgb("#3FB950"):Color.FromArgb("#484F58"),CornerRadius=4,FontSize=isPinned?10:14,HeightRequest=20,WidthRequest=EditW,Padding=0,VerticalOptions=LayoutOptions.Start,BorderColor=isPinned?Color.FromArgb("#3FB950"):Color.FromArgb("#30363D"),BorderWidth=1};pinBtn.Clicked+=(_,_)=>TogglePin(game.GameId);row.Add(pinBtn,3,0);return row;
    }
    private async Task ShowDetailAsync(GameTimelineRow g){await DisplayAlert(g.DisplayName,$"Started: {g.StartDate:dd MMM yyyy}\nLast active: {g.EndDate:dd MMM yyyy}\nRecords: {g.TotalLogs:N0}\nFocus blocks: {g.SubBlocks.Count}","OK");}
    private async Task<(List<GameTimelineRow>,List<Game>)> BuildTimelineDataAsync(){var user=_auth.CurrentUsername;var c=await _db.GetConnectionAsync();var games=await c.Table<Game>().Where(g=>g.Username==user).ToListAsync();var logs=await c.Table<ExpLog>().Where(e=>e.Username==user).ToListAsync();var by=logs.GroupBy(e=>e.Game).ToDictionary(g=>g.Key,g=>g.OrderBy(e=>e.LoggedAt).ToList());var rows=new List<GameTimelineRow>();foreach(var game in games.OrderBy(g=>g.CreatedAt)){var ls=by.TryGetValue(game.GameId,out var found)?found:new();var start=game.CreatedAt.ToLocalTime();var end=ls.Count>0?ls.Last().LoggedAt.ToLocalTime():game.LastVisitedAt?.ToLocalTime()??start;var blocks=new List<SubBlockData>();if(_lifePathService!=null){var bs=await _lifePathService.GetBlocksForGameAsync(user,game.GameId);blocks=bs.Select(b=>new SubBlockData(b.Id,b.ParentBlockId,b.Label,b.StartDate,b.EndDate,b.Level,b.ColorHex)).ToList();}rows.Add(new GameTimelineRow{GameId=game.GameId,DisplayName=game.DisplayName,StartDate=start,EndDate=game.LifePathEndedAt?.ToLocalTime()??end,EndedAt=game.LifePathEndedAt?.ToLocalTime(),IsActive=game.IsActive,TotalLogs=ls.Count,Periods=Periods(ls,start),SubBlocks=blocks});}return(rows,games);}
    private static List<ActivePeriod> Periods(List<ExpLog> logs,DateTime start){var r=new List<ActivePeriod>();if(logs.Count==0){r.Add(new ActivePeriod{Start=start,End=start,IsGap=true});return r;}var s=logs.OrderBy(l=>l.LoggedAt).ToList();var ps=s[0].LoggedAt.ToLocalTime();var pe=ps;int n=1;for(int i=1;i<s.Count;i++){var cur=s[i].LoggedAt.ToLocalTime();if((cur-pe).TotalDays>30){r.Add(new ActivePeriod{Start=ps,End=pe,LogCount=n});r.Add(new ActivePeriod{Start=pe,End=cur,IsGap=true});ps=cur;pe=cur;n=1;}else{pe=cur;n++;}}r.Add(new ActivePeriod{Start=ps,End=pe,LogCount=n});return r;}
    internal class GameTimelineRow{public string GameId{get;set;}="";public string DisplayName{get;set;}="";public DateTime StartDate{get;set;}public DateTime EndDate{get;set;}public DateTime? EndedAt{get;set;}public bool IsActive{get;set;}public int TotalLogs{get;set;}public List<ActivePeriod> Periods{get;set;}=new();public List<SubBlockData> SubBlocks{get;set;}=new();}
    internal class ActivePeriod{public DateTime Start{get;set;}public DateTime End{get;set;}public bool IsGap{get;set;}public int LogCount{get;set;}}
}

public class AxisDrawable:IDrawable
{
    private readonly DateTime _start,_end;private readonly double _span;private readonly float _barW;private readonly List<(float X,string Label)> _ticks=new();
    public AxisDrawable(DateTime start,DateTime end,double span,float barW){_start=start;_end=end;_span=span;_barW=barW;bool decade=(end-start).TotalDays>2000;bool year=(end-start).TotalDays>60;if(decade){for(int y=start.Year/10*10;y<=end.Year+10;y+=10)Add(new DateTime(y,1,1),y.ToString());}else if(year){for(int y=start.Year;y<=end.Year+1;y++)Add(new DateTime(y,1,1),y.ToString());}else{for(var d=new DateTime(start.Year,start.Month,1);d<=end;d=d.AddMonths(1))Add(d,d.ToString("MMM yy"));}}
    private void Add(DateTime date,string label){float x=(float)((date-_start).TotalDays/_span*_barW);if(x>=0&&x<=_barW)_ticks.Add((x,label));}
    public void Draw(ICanvas canvas,RectF dirty){canvas.StrokeColor=Color.FromArgb("#30363D");canvas.StrokeSize=1;canvas.DrawLine(0,18,_barW,18);foreach(var(x,label)in _ticks){canvas.StrokeColor=Color.FromArgb("#484F58");canvas.DrawLine(x,12,x,18);canvas.FontColor=Color.FromArgb("#8B949E");canvas.FontSize=9;canvas.DrawString(label,x+2,0,50,12,HorizontalAlignment.Left,VerticalAlignment.Bottom);}}
}
