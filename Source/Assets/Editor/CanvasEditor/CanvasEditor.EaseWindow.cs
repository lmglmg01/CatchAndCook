using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>실제 DOTween 곡선을 비교하고 선택하는 독립 창입니다.</summary>
public sealed class UIEditorEaseWindow : EditorWindow
{
    public sealed class Curve : VisualElement
    {
        public Ease ease;
        public float progress;
        public Curve(Ease value)
        {
            ease = value; AddToClassList("ease-graph");
            generateVisualContent += context =>
            {
                float w = contentRect.width - 20, h = contentRect.height - 20;
                if (w <= 0 || h <= 0) return;
                var p = context.painter2D;
                Vector2 Point(float x, float y) => new(10 + x * w, 10 + h * (.85f - y * .7f));
                p.lineWidth = 1; p.strokeColor = new Color(.5f,.55f,.6f,.4f);
                p.BeginPath(); p.MoveTo(Point(0,0)); p.LineTo(Point(1,1)); p.Stroke();
                p.lineWidth = 2; p.strokeColor = new Color(.35f,.7f,1);
                p.BeginPath();
                for (int i = 0; i <= 100; i++)
                { float x = i / 100f; var point = Point(x, DOVirtual.EasedValue(0,1,x,ease)); if (i == 0) p.MoveTo(point); else p.LineTo(point); }
                p.Stroke();
                var dot = Point(progress,DOVirtual.EasedValue(0,1,progress,ease));
                p.fillColor = new Color(1,.8f,.3f); p.BeginPath(); p.Arc(dot,3,0,360); p.Fill();
            };
        }
    }
    Ease _chosen;
    float _duration;
    Action<Ease> _confirm;
    readonly List<(Curve graph, VisualElement dot, double start)> _cards = new();
    Curve _detail;
    Label _name, _time;
    VisualElement _sample, _track;
    PopupField<string> _compare;
    bool _loop = true;
    double _started;

    public static void Open(Ease selected, float duration, Action<Ease> confirm)
    {
        var window = CreateInstance<UIEditorEaseWindow>();
        window.titleContent = new GUIContent("Ease 선택 · DOTween"); window.minSize = new Vector2(800,580);
        window._chosen = selected; window._duration = Mathf.Max(.01f,duration); window._confirm = confirm;
        window.position = new Rect(250,160,920,680); window.ShowUtility();
    }
    void OnEnable() { EditorApplication.update += Tick; }
    void OnDisable() { EditorApplication.update -= Tick; }
    public void CreateGUI()
    {
        var root = rootVisualElement; root.AddToClassList("canvas-editor");
        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CanvasEditor/CanvasEditor.uss"); if(style!=null)root.styleSheets.Add(style);
        var row = new VisualElement(); row.AddToClassList("row"); row.AddToClassList("ease-body"); root.Add(row);
        var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("ease-gallery"); row.Add(scroll);
        var heading = new Label("In · 서서히 출발          Out · 부드럽게 도착          InOut · 양쪽 완화"); heading.AddToClassList("director-note"); scroll.Add(heading);
        AddFamily(scroll,"Linear",new[]{Ease.Linear});
        foreach(string family in new[]{"Sine","Quad","Cubic","Quart","Quint","Expo","Circ","Back","Elastic","Bounce"})
            AddFamily(scroll,family,new[]{(Ease)Enum.Parse(typeof(Ease),"In"+family),(Ease)Enum.Parse(typeof(Ease),"Out"+family),(Ease)Enum.Parse(typeof(Ease),"InOut"+family)});
        var detail = new VisualElement(); detail.AddToClassList("ease-detail"); row.Add(detail);
        _name = new Label(); _name.AddToClassList("panel-title"); detail.Add(_name);
        _detail = new Curve(_chosen); _detail.AddToClassList("ease-large-graph"); detail.Add(_detail);
        detail.Add(new Label("선택한 곡선의 움직임"));
        _track = new VisualElement(); _track.AddToClassList("ease-track"); detail.Add(_track);
        _sample = new VisualElement(); _sample.AddToClassList("ease-sample"); _track.Add(_sample);
        _time = new Label(); _time.AddToClassList("director-note"); detail.Add(_time);
        _compare = new PopupField<string>("비교할 표시",new List<string>{"위치","크기","투명도","회전"},0); _compare.AddToClassList("stacked-field"); detail.Add(_compare);
        var replay = new Button(()=>_started=EditorApplication.timeSinceStartup){text="다시 재생"}; detail.Add(replay);
        var loop = new Toggle("반복 재생"){value=true};loop.RegisterValueChangedCallback(e=>_loop=e.newValue);detail.Add(loop);
        var note = new Label("카드 위에 마우스를 올리면 짧게 재생합니다. 클릭하면 우측에서 자세히 비교합니다.\n그래프와 재생 값 모두 DOTween 계산을 사용합니다.");note.AddToClassList("director-note");detail.Add(note);
        var buttons=new VisualElement();buttons.AddToClassList("row");buttons.AddToClassList("ease-footer");root.Add(buttons);
        buttons.Add(new Button(Close){text="취소"});
        var apply=new Button(()=>{_confirm?.Invoke(_chosen);Close();}){text="곡선 선택 확정"};apply.AddToClassList("primary-button");buttons.Add(apply);
        root.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){Close();e.StopPropagation();}});
        Choose(_chosen);
    }
    void AddFamily(VisualElement parent,string family, Ease[] values)
    {
        var name=new Label(family);name.AddToClassList("ease-family");parent.Add(name);
        var row=new VisualElement();row.AddToClassList("row");parent.Add(row);
        foreach(var ease in values)
        {
            var card=new Button(()=>Choose(ease));card.AddToClassList("ease-card");card.name="ease-"+ease;row.Add(card);
            var graph=new Curve(ease){pickingMode=PickingMode.Ignore};card.Add(graph);
            card.Add(new Label(ease.ToString()){pickingMode=PickingMode.Ignore});
            var track=new VisualElement{pickingMode=PickingMode.Ignore};track.AddToClassList("ease-small-track");card.Add(track);
            var dot=new VisualElement{pickingMode=PickingMode.Ignore};dot.AddToClassList("ease-dot");track.Add(dot);
            int index=_cards.Count;_cards.Add((graph,dot,-1));
            card.RegisterCallback<PointerEnterEvent>(_=>_cards[index]=(graph,dot,EditorApplication.timeSinceStartup));
            card.RegisterCallback<PointerLeaveEvent>(_=>{_cards[index]=(graph,dot,-1);graph.progress=0;graph.MarkDirtyRepaint();dot.style.left=0;});
        }
    }
    void Choose(Ease ease)
    {
        _chosen=ease;_started=EditorApplication.timeSinceStartup;
        if(_name==null)return;_name.text=ease.ToString();_detail.ease=ease;_detail.MarkDirtyRepaint();
        _time.text=$"지속시간 {_duration:0.00}초 · 점선은 일정한 속도";
        foreach(var card in rootVisualElement.Query<Button>(className:"ease-card").ToList())card.EnableInClassList("selected-item",card.name=="ease-"+ease);
    }
    void Tick()
    {
        if(_sample==null)return;
        double now=EditorApplication.timeSinceStartup;
        foreach(var card in _cards)
        {
            if(card.start<0)continue;float t=Mathf.Clamp01((float)(now-card.start)/.8f);card.graph.progress=t;card.graph.MarkDirtyRepaint();
            card.dot.style.left=Mathf.Max(0,card.dot.parent.layout.width-8)*DOVirtual.EasedValue(0,1,t,card.graph.ease);
        }
        float elapsed=(float)(now-_started),phase=_loop?elapsed%(_duration+.5f):elapsed,t2=Mathf.Clamp01(phase/_duration);
        float value=DOVirtual.EasedValue(0,1,t2,_chosen);_detail.progress=t2;_detail.MarkDirtyRepaint();
        _sample.style.left=10;_sample.style.opacity=1;_sample.style.scale=new Scale(Vector3.one);_sample.style.rotate=new Rotate(0);
        switch(_compare.index)
        {
            case 0:_sample.style.left=10+Mathf.Max(0,_track.layout.width-65)*value;break;
            case 1:_sample.style.left=Mathf.Max(10,(_track.layout.width-40)/2);_sample.style.scale=new Scale(Vector3.one*Mathf.LerpUnclamped(.5f,1.3f,value));break;
            case 2:_sample.style.left=Mathf.Max(10,(_track.layout.width-40)/2);_sample.style.opacity=Mathf.Clamp01(value);break;
            case 3:_sample.style.left=Mathf.Max(10,(_track.layout.width-40)/2);_sample.style.rotate=new Rotate(Mathf.LerpUnclamped(-35,35,value));break;
        }
    }
}
