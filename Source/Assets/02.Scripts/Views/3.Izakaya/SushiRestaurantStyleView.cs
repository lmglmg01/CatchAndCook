using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;

/// <summary>초밥집 구상도의 표시 규칙만 적용합니다. 데이터와 버튼의 기존 연결은 변경하지 않습니다.</summary>
public class SushiRestaurantStyleView : MonoBehaviour
{
    [SerializeField] Sprite[] _art;
    [SerializeField] TMP_FontAsset _bold, _regular;
    [SerializeField] Sprite _roundedPanel;
    public Sprite RoundedPanelSprite => _roundedPanel;
    static Color Brown => new Color32(116,76,65,255);
    static Color Cream => new Color32(253,247,228,255);
    static Color Dark => new Color32(166,120,107,255);
    Transform Book => transform.Find("MenuBook");
    Sprite Art(string name)
    {
        string replacement = name switch {
            "recipebook_menu_level_round18" => "Level_Badge_Rd19",
            "restaurant_open_temporary" => "Open", "restaurant_close_temporary" => "Closed",
            "Sparkle_Left" => "Todays_Menu_Sparkle_Left", "Sparkle_Right" => "Todays_Menu_Sparkle_Right",
            "settlement_receipt_background_left" => "settlement_receipt_background_left1",
            "settlement_receipt_background_right" => "settlement_receipt_background_right1",
            _ => name };
        return System.Array.Find(_art, s => s != null && s.name == replacement)
            ?? System.Array.Find(_art, s => s != null && s.name == name);
    }

    /// <summary>기존 오브젝트의 중앙 기준 위치와 크기만 바꿉니다.</summary>
    static void Place(Transform t,float x,float y,float w,float h)
    {
        if (!(t is RectTransform r)) return;
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(w,h);
    }
    static void Active(Transform t,bool value) { if(t!=null)t.gameObject.SetActive(value); }
    /// <summary>장식이 없을 때만 추가하며 기존 참조는 유지합니다.</summary>
    Transform Ensure(Transform p,string name)
    {
        var t=p.Find(name); if(t!=null)return t;
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(p,false); return go.transform;
    }
    Image Picture(Transform t,string name,Color? color=null)
    {
        var image=t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
        image.sprite=name=="Panel"?_roundedPanel:Art(name);
        // 전달받은 이미지 리소스는 원본 색상을 유지하며, 단색 도형만 지정 색상을 사용합니다.
        image.color=name!="Panel" && !string.IsNullOrEmpty(name)?Color.white:color??Color.white;
        image.type=image.sprite!=null && image.sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;
        image.raycastTarget=t.GetComponent<Selectable>()!=null || t.GetComponent<SushiIngredientSlotView>()!=null || t.GetComponent<IngredientSourceIconView>()!=null;
        var selectable=t.GetComponent<Selectable>();
        if(selectable!=null && image.sprite!=null && name!="Panel"){selectable.transition=Selectable.Transition.None;image.canvasRenderer.SetColor(Color.white);}
        return image;
    }
    void Label(Transform t,float size,bool regular=false,Color? color=null,string value=null)
    {
        if(t==null)return;
        var text=t.GetComponent<TMP_Text>()??t.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=regular?_regular:_bold; text.fontStyle=FontStyles.Normal;
        text.fontSize=size; text.enableAutoSizing=false; text.color=color??Brown;
        text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        if(value!=null)text.text=value;
    }
    void Decor(Transform p,string name,string art,float x,float y,float w,float h,Color? color=null)
    { var t=Ensure(p,name); Place(t,x,y,w,h); Picture(t,art,color); t.SetAsFirstSibling(); }
    void Button(Transform t,float x,float y,float w,float h,string label)
    {
        Place(t,x,y,w,h); Picture(t,"Panel",new Color32(73,119,255,255));
        var shadow=Ensure(t.parent,t.name+"Shadow");Place(shadow,x,y-9,w,h);Picture(shadow,"Panel",new Color32(45,64,201,255));shadow.SetAsLastSibling();t.SetAsLastSibling();
        Active(shadow,t.gameObject.activeSelf);
        var l=t.Find("Label"); Place(l,0,0,w,h); Label(l,42,false,Cream,label);
    }
    /// <summary>스크롤 손잡이와 레일의 폭을 동일하게 맞추고 콘텐츠와 분리합니다.</summary>
    void Scroll(ScrollRect s,float width)
    {
        if(s==null || s.verticalScrollbar==null)return;
        var bar=s.verticalScrollbar; var r=(RectTransform)bar.transform;
        r.anchorMin=new Vector2(1,0); r.anchorMax=new Vector2(1,1); r.pivot=new Vector2(1,.5f);
        r.anchoredPosition=Vector2.zero; r.sizeDelta=new Vector2(width,0);
        Picture(r,"scrollbar_outside_round5");
        Picture(bar.handleRect,"scrollbar_inside_round5");
        bar.handleRect.sizeDelta=new Vector2(0,0);
        bar.transition=Selectable.Transition.None;
        s.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
    }
    /// <summary>탭 전환으로 기존 View가 배치를 갱신한 뒤 새 구상도의 표시를 적용합니다.</summary>
    public void ApplyBook(int mode)
    {
        if(Book==null || _art==null)return;
        Book.GetComponent<Image>().color=new Color(0,0,0,.6f);
        var cover=Book.Find("BookCover"); Place(cover,0,-50.5f,1550,879); Picture(cover,"Book_Back");
        var front=Ensure(Book,"BookFront"); Place(front,0,-17.5f,1464,867); Picture(front,"Book_Front");
        // 제공받은 중앙선 원본을 Single / Point 설정 그대로 책 앞표지에 배치합니다.
        var seam=Ensure(front,"BookCenterLine");Place(seam,-.5f,.5f,8,812);
        var seamImage=Picture(seam,"recipebook_center_line");
        seamImage.preserveAspect=true;seamImage.raycastTarget=false;
        Active(seam,seamImage.sprite!=null);
        Active(Book.Find("LeftPage"),false); Active(Book.Find("RightPage"),false);
        var title=Book.Find("BookTitle"); Place(title,-365,mode==0?345:mode==2?326:331,mode!=3?320:324,80); Label(title,42,false,Cream);
        Decor(Book,"PageRibbon",mode==0?"recipe_label_orange":mode==1?"recipe_label_red":title.GetComponent<TMP_Text>().text.Contains("일반")?"recipe_lable_blue":"recipe_lable_green",-365,mode==0?345:mode==2?326:331,mode!=3?320:324,mode!=3?80:88);
        if(mode==0)
        {
            // 하단 그림자를 제외한 주황색 면의 중앙에 실제 글자 영역을 맞춥니다.
            Place(title,-365,347.5f,320,80);
            var todayHeading=title.GetComponent<TMP_Text>();
            todayHeading.alignment=TextAlignmentOptions.Midline;todayHeading.margin=Vector4.zero;
        }
        if(mode==1)
        {
            // 하단 그림자를 제외한 빨간색 면에 요리강화 글자의 위아래 여백을 맞춥니다.
            Place(title,-365,333.5f,320,80);
            var upgradeHeading=title.GetComponent<TMP_Text>();
            upgradeHeading.alignment=TextAlignmentOptions.Midline;upgradeHeading.margin=Vector4.zero;
        }
        if(mode==2)
        {
            // 원본 리본 324×88에는 그림자가 포함됩니다. 녹색 면의 실제 크기를 가이드의 약 320×80에 맞춥니다.
            Place(Book.Find("PageRibbon"),-363,328,324,88);
            Place(title,-363.5f,329.5f,320,80);
            var basicTitle=title.GetComponent<TMP_Text>();
            basicTitle.fontSize=40;basicTitle.enableAutoSizing=false;basicTitle.margin=Vector4.zero;
            basicTitle.alignment=TextAlignmentOptions.Midline;
        }
        Book.Find("PageRibbon").SetSiblingIndex(cover.GetSiblingIndex()+1);
        Decor(Book,"TitleSparkleLeft","Sparkle_Left",-575,345,81,70);Book.Find("TitleSparkleLeft").SetSiblingIndex(cover.GetSiblingIndex()+1);Active(Book.Find("TitleSparkleLeft"),mode==0);
        Decor(Book,"TitleSparkleRight","Sparkle_Right",-157,345,74,69);Book.Find("TitleSparkleRight").SetSiblingIndex(cover.GetSiblingIndex()+1);Active(Book.Find("TitleSparkleRight"),mode==0);
        Active(Book.Find("RecipeCategoryDivider"),false);
        Place(Book.Find("RecipeCategory"),-115,331,130,40);
        // 선상 UI와 같은 좌상단 뒤로가기 배치입니다. 기존 버튼과 클릭 연결은 유지합니다.
        var back = Book.Find("CloseButton") as RectTransform;
        if (back != null)
        {
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f); back.pivot = new Vector2(.5f, .5f);
            back.anchoredPosition = new Vector2(170f, -88f);
            back.sizeDelta = new Vector2(240f, 64f);
            var backLabel = back.Find("Label")?.GetComponent<TMP_Text>();
            if (backLabel != null) backLabel.text = "뒤로가기";
        }
        var list=Book.Find("RecipeCatalog"); Place(list,mode==0?363:-366,mode==0?139.5f:-60,mode==0?630:600,mode==0?390:620);
        Picture(list,"recipebook_background_round35");
        var listTitle=list.Find("Title"); Active(listTitle,mode==0); Place(listTitle,0,230,600,50); Label(listTitle,mode==0?34:30);
        var scroll=list.Find("RecipeScroll"); Place(scroll,0,0,mode==0?630:600,mode==0?390:620);
        var sr=scroll.GetComponent<ScrollRect>(); Place(sr.viewport,0,0,mode==0?582:600,mode==0?342:620); Scroll(sr,mode==0?12:20);
        // 가장자리 슬롯의 호버 효과가 목록 안쪽 여백까지 표시되도록 합니다.
        var clip=sr.viewport.GetComponent<RectMask2D>();
        if(clip!=null)clip.padding=mode==0?new Vector4(-16,-7,-16,-7):mode==1?new Vector4(0,31,0,0):Vector4.zero;
        if(mode==0 && sr.verticalScrollbar!=null){var rail=(RectTransform)sr.verticalScrollbar.transform;rail.anchorMin=rail.anchorMax=new Vector2(1,.5f);rail.pivot=new Vector2(.5f,.5f);rail.anchoredPosition=new Vector2(23,0);rail.sizeDelta=new Vector2(12,360);}
        if(mode==1 && sr.verticalScrollbar!=null)
        {
            var rail=(RectTransform)sr.verticalScrollbar.transform;rail.anchorMin=rail.anchorMax=new Vector2(1,.5f);rail.pivot=new Vector2(.5f,.5f);rail.anchoredPosition=new Vector2(32,27);rail.sizeDelta=new Vector2(20,350);
            // 가이드처럼 폭20px의 양 끝이 반원이 되도록 고해상도 9-slice를 사용합니다.
            var railImage=Picture(rail,"Panel",new Color32(234,201,179,255));railImage.pixelsPerUnitMultiplier=2.2f;
            var handleImage=Picture(sr.verticalScrollbar.handleRect,"Panel",new Color32(166,120,107,255));handleImage.pixelsPerUnitMultiplier=2.2f;
        }
        sr.content.anchorMin=new Vector2(0,1);sr.content.anchorMax=Vector2.one;sr.content.pivot=new Vector2(.5f,1);
        sr.content.sizeDelta=new Vector2(0,sr.content.sizeDelta.y);sr.content.anchoredPosition=Vector2.zero;
        var grid=sr.content.GetComponent<GridLayoutGroup>();
        grid.cellSize=new Vector2(mode==0?106:100,mode==0?106:100); grid.spacing=new Vector2(12,12); grid.constraintCount=5;
        grid.padding=mode==0?new RectOffset():new RectOffset(30,22,35,37);grid.childAlignment=TextAnchor.UpperLeft;
        var stock=Application.isPlaying?UnityEngine.Object.FindFirstObjectByType<SushiListManager>():null;
        foreach(var slot in list.GetComponentsInChildren<SushiRecipeSlotView>(true))
        {
            Picture(slot.transform,"popup_text_background_round18",Color.white).pixelsPerUnitMultiplier=.6f;
            Place(slot.transform.Find("FoodIcon"),0,0,125,120);
            if(mode==0)FitMenuIconWithMargin(slot.transform.Find("FoodIcon"),0,0,106,106);
            if(mode==1)FitMenuIconWithMargin(slot.transform.Find("FoodIcon"),0,0,100,100);
            Active(slot.transform.Find("Name"),false); var level=slot.transform.Find("Level"); Active(level,mode==1);
            Place(level,-38,40,40,40); Label(level,mode==1?20:24,false,Cream);
            if(mode==1){var levelText=level.GetComponent<TMP_Text>();levelText.alignment=TextAlignmentOptions.Midline;levelText.margin=Vector4.zero;}
            Decor(slot.transform,"LevelBadge","Level_Badge",-38,40,40,40); Active(slot.transform.Find("LevelBadge"),mode==1);
            slot.transform.Find("LevelBadge").SetAsLastSibling();level.SetAsLastSibling();
            var recipeName=slot.transform.Find("Name").GetComponent<TMP_Text>().text;
            var entries=GetComponent<IjakayaView>().MenuView.CurrentPresentation?.Recipes;
            var entry=entries==null?null:System.Array.Find(entries,item=>item!=null && item.RecipeId==slot.RecipeId);
            int remaining=0;
            if(stock!=null)foreach(var menu in stock.TodayMenu)if(menu!=null && menu.Menu!=null && menu.Menu.Id==slot.RecipeId)remaining+=menu.Remaining;
            if(entry!=null && entry.RemainingQuantity>=0)remaining=entry.RemainingQuantity;
            var count=Ensure(slot.transform,"OwnedQuantity");Place(count,27,-31,62,32);Label(count,24,false,Cream,$"x{remaining}");count.SetAsLastSibling();Active(count,mode==1 && remaining>0);
            Decor(slot.transform,"UpgradeAvailable","Upgrade_Sign",30,25,20,30);var upgradeArrow=slot.transform.Find("UpgradeAvailable");upgradeArrow.SetAsLastSibling();if(upgradeArrow.GetComponent<RecipeUpgradeArrowMotion>()==null)upgradeArrow.gameObject.AddComponent<RecipeUpgradeArrowMotion>();Active(upgradeArrow,mode==1 && entry!=null && entry.CanUpgrade);
        }
        foreach(var image in Book.GetComponentsInChildren<Image>(true))
            if(image.name.StartsWith("EmptyRecipeCellTemplate"))Picture(image.transform,"Menu_empty_slot_round30");
        var detail=Book.Find("SelectedRecipeDetail"); Place(detail,mode==0?363:368,mode==0?-230.5f:-11,mode==0?630:624,mode==0?325:712);
        Picture(detail,"recipebook_background_round35"); detail.GetComponent<Image>().enabled=true;
        Active(detail.Find("TodayDetailBackdrop"),false); Active(detail.Find("UpgradeIngredientBackgrounds"),false);
        Active(detail.Find("upgradeDescriptionBackdrop"),false);
        var backdrop=detail.Find(mode==0?"TodayFoodBackdrop":"upgradeFoodBackdrop");
        Place(backdrop,mode==0?-155:-169.5f,mode==0?44:207.5f,mode==0?188:207,mode==0?188:207); Picture(backdrop,"menu_slot_icon_round35",Color.white);
        Picture(backdrop,"popup_text_background_round18").pixelsPerUnitMultiplier=18f/35f;
        Place(detail.Find("FoodIcon"),mode==0?-155:-169.5f,mode==0?44:207.5f,mode==0?188:270,mode==0?188:270);
        if(mode==0)FitTodayMenuIcon(detail.Find("FoodIcon"),-155,44);
        if(mode==1)FitTodayMenuIcon(detail.Find("FoodIcon"),-169.5f,207.5f,175);
        Place(detail.Find("Name"),mode==0?134:130,mode==0?109:249,mode==0?335:320,mode==0?50:60); Label(detail.Find("Name"),mode==1?38:34);
        var detailName=detail.Find("Name").GetComponent<TMP_Text>();detailName.enableAutoSizing=true;detailName.fontSizeMin=22;detailName.fontSizeMax=mode==1?38:34;detailName.enableWordWrapping=mode!=0;
        detail.Find("Name").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        Place(detail.Find("Description"),mode==0?134:130,mode==0?-7.5f:172,mode==0?335:320,mode==0?72:100); Label(detail.Find("Description"),mode==0?20:22,true,Dark);
        detail.Find("Description").GetComponent<TMP_Text>().overflowMode=TextOverflowModes.Ellipsis;
        detail.Find("Description").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.TopLeft;
        Place(detail.Find("LevelPrice"),mode==0?23:-226,mode==0?62:310,mode==1?110:108,40); Label(detail.Find("LevelPrice"),24,false,Cream);
        Decor(detail,"LevelBackground","recipebook_menu_level_round18",mode==0?23:-226,mode==0?62:310,mode==1?110:108,40);
        detail.Find("LevelBackground").SetAsLastSibling();detail.Find("LevelPrice").SetAsLastSibling();
        Place(detail.Find("RecipePriceCoin"),mode==0?115:-200,mode==0?62:-290,mode==1?65:58,mode==1?65:58);
        Place(detail.Find("RecipePriceCoin/SalePrice"),mode==1?80:75,0,mode==1?130:100,mode==1?40:42); Label(detail.Find("RecipePriceCoin/SalePrice"),mode==1?30:28);
        Decor(detail,"PriceBackground","recipebook_price_round18",mode==0?181:-120,mode==0?62:-290,mode==1?150:140,mode==1?40:42);
        if(mode==0){Place(detail.Find("RecipePriceCoin/SalePrice"),68,0,100,42);detail.Find("RecipePriceCoin").SetAsLastSibling();}
        Button(detail.Find("RegisterButton"),0,-100.5f,210,72,"등록");
        if(mode==0)
        {
            var registerLabel=detail.Find("RegisterButton/Label").GetComponent<TMP_Text>();
            registerLabel.fontSize=36;registerLabel.alignment=TextAlignmentOptions.Midline;registerLabel.margin=Vector4.zero;
        }
        Button(detail.Find("UpgradeButton"),124,-289,250,80,"강화");
        if(mode==1){var upgradeLabel=detail.Find("UpgradeButton/Label").GetComponent<TMP_Text>();upgradeLabel.fontSize=40;upgradeLabel.alignment=TextAlignmentOptions.Midline;upgradeLabel.margin=Vector4.zero;}
        detail.Find("UpgradeButton").GetComponent<Image>().pixelsPerUnitMultiplier=.88f;
        Place(detail.Find("UpgradeButtonShadow"),124,-299,250,80);detail.Find("UpgradeButtonShadow").GetComponent<Image>().pixelsPerUnitMultiplier=.88f;
        detail.Find("UpgradeButton").GetComponent<Image>().color=new Color32(255,80,89,255);
        detail.Find("UpgradeButtonShadow").GetComponent<Image>().color=new Color32(164,44,56,255);
        Active(detail.Find("RegisterButtonShadow"),mode==0 && detail.Find("RegisterButton").GetComponent<Button>().interactable);Active(detail.Find("UpgradeButtonShadow"),mode==1);
        if(mode==0){var register=detail.Find("RegisterButton").GetComponent<Button>();register.transition=Selectable.Transition.None;register.image.canvasRenderer.SetColor(Color.white);if(!register.interactable)Picture(register.transform,"Button_Disabled");}
        detail.Find("UpgradeButton").GetComponent<Button>().transition=Selectable.Transition.None;
        detail.Find("UpgradeButton").GetComponent<Image>().canvasRenderer.SetColor(Color.white);
        if(!detail.Find("UpgradeButton").GetComponent<Button>().interactable){Picture(detail.Find("UpgradeButton"),"Button_Disabled");Active(detail.Find("UpgradeButtonShadow"),false);}
        var change=Ensure(detail,"LevelChange"); Place(change,2.5f,39,525,66); Picture(change,"menu_upgrade_change_background_round18");
        Active(change,mode==1); var changeText=Ensure(change,"Label"); Place(changeText,0,0,505,64);
        var data=GetComponent<IjakayaView>().MenuView.SelectedRecipe;
        if(mode==0 || mode==1)Label(detail.Find("LevelPrice"),24,false,Cream,data==null?string.Empty:$"레벨 {data.Level}");
        if(mode==1)Label(detail.Find("RecipePriceCoin/SalePrice"),30,false,Brown,data?.UpgradeCost??string.Empty);
        Active(changeText,false);
        var levelHeading=Ensure(change,"LevelHeading");Place(levelHeading,-135,0,100,60);Label(levelHeading,26,false,Cream,"레벨");
        var currentLevel=Ensure(change,"CurrentLevel");Place(currentLevel,-20,0,70,60);Label(currentLevel,26,true,Cream,data==null?"—":data.Level.ToString());
        var arrow=Ensure(change,"Arrow");Place(arrow,70,0,60,60);Label(arrow,36,false,new Color32(159,216,63,255),"››");
        var nextLevel=Ensure(change,"NextLevel");Place(nextLevel,140,0,90,60);Label(nextLevel,26,false,new Color32(159,216,63,255),data==null?"—":data.Level>=SushiUpgradeData.MaxLevel?"최대":(data.Level+1).ToString());
        var heading=Ensure(detail,"IngredientsHeading"); Place(heading,0,mode==1?-38:-40,500,50); Label(heading,28,false,Brown,"필요 재료"); Active(heading,mode==1);
        var ingredients=detail.GetComponentsInChildren<SushiIngredientSlotView>(true);
        for(int i=0;i<4;i++){Decor(detail,"MaterialPlaceholder"+i,"menu_mini_slot_icon_round30",-211+i*140,mode==1?-127:-129,124,124,Color.white);Active(detail.Find("MaterialPlaceholder"+i),mode==1);}
        for(int i=0;i<ingredients.Length;i++){Ingredient(ingredients[i].transform,-211+i*140,mode==1?-127:-129,124);if(mode==1){Picture(ingredients[i].transform,"popup_text_background_round18").pixelsPerUnitMultiplier=.6f;FitMenuIconWithMargin(ingredients[i].transform.Find("Icon"),0,0,124,124);}}
        if(mode==1)for(int i=0;i<4;i++)Picture(detail.Find("MaterialPlaceholder"+i),"popup_text_background_round18").pixelsPerUnitMultiplier=.6f;
        TodayCards(); BasicCards();
        ApplyBookmarks(mode);
        foreach(var s in Book.GetComponentsInChildren<ScrollRect>(true))if(s!=sr)Scroll(s,13);
        // 레시피 목록은 휠·드래그를 유지하고 레일과 손잡이 그래픽만 숨깁니다.
        var recipeScroll=Book.Find("BasicMenuSummary").GetComponentInChildren<ScrollRect>(true);
        if(recipeScroll!=null && recipeScroll.verticalScrollbar!=null)
            foreach(var graphic in recipeScroll.verticalScrollbar.GetComponentsInChildren<Graphic>(true)){graphic.enabled=false;graphic.raycastTarget=false;}
    }
    /// <summary>재료 이름은 툴팁에 유지하고 카드에는 아이콘과 보유/소모 수량을 표시합니다.</summary>
    void Ingredient(Transform t,float x,float y,float size)
    {
        Place(t,x,y,size,size); Picture(t,"menu_mini_slot_icon_round30",Color.white);
        Place(t.Find("Icon"),0,0,size-10,size-10); Active(t.Find("Name"),false);
        Place(t.Find("Amount"),0,-size/2-20,size,30); Label(t.Find("Amount"),24,false,Cream);
        Decor(t,"AmountBackground","menu_upgrade_meterial_need_background_round15",0,-size/2-20,size,30);
        var pin=Ensure(t,"SourcePin"); if(pin!=null){Place(pin,-size/2+20,size/2-23,24,30); Picture(pin,"location_icon"); Active(pin.Find("Center"),false);Active(pin,true);pin.SetAsLastSibling();}
    }
    /// <summary>기존 직원 목록과 상세 참조를 유지하며 책 UI의 두 페이지에 배치합니다.</summary>
    public void ApplyStaff()
    {
        var view = GetComponent<IjakayaView>()?.StaffView;
        if (view == null || Book == null) return;
        var root = view.transform;
        var title = Book.Find("BookTitle"); Place(title,-365,333.5f,320,80);Label(title,42,false,Cream,"직원");
        var staffHeading=title.GetComponent<TMP_Text>();staffHeading.alignment=TextAlignmentOptions.Midline;staffHeading.margin=Vector4.zero;
        Place(Book.Find("PageRibbon"),-365,331,320,80);
        Picture(Book.Find("PageRibbon"),"recipe_lable_blue");
        Active(root.Find("Title"),false); Active(root.Find("CloseButton"),false);
        var rootImage = root.GetComponent<Image>(); if(rootImage!=null){rootImage.color=Color.clear;rootImage.raycastTarget=false;}
        var list=root.Find("StaffList");Place(list,-365,-57,610,620);
        var listImage=list.GetComponent<Image>();if(listImage!=null)listImage.enabled=false;
        Active(list.Find("Title"),false);Active(list.Find("Divider"),false);
        var staff=view.Staff;
        for(int i=0;i<6;i++)
        {
            // 세 번째 이후 칸은 직원 모델이 아직 없으므로 실제 해금이나 강화를 연결하지 않습니다.
            var slot=i<2?list.Find("StaffSlot"+(i+1)):Ensure(list,"FutureStaffSlot"+(i+1));
            int displayIndex = i;
            Active(slot,true);Place(slot,-210+(displayIndex%3)*210,displayIndex<3?155:-155,190,290);
            Picture(slot,"todays_menu_slot");
            var item=staff!=null&&i<staff.Length?staff[i]:null;
            var outline=slot.GetComponent<Outline>()??slot.gameObject.AddComponent<Outline>();
            outline.effectColor=Cream;outline.effectDistance=new Vector2(6,-6);outline.useGraphicAlpha=true;
            outline.enabled=false;
            // UI팀이 제공한 6px 선택 테두리 리소스를 사용합니다.
            if(item!=null && item.StaffId==view.SelectedStaffId) Picture(slot,"todays_menu_select_slot");
            var portrait=slot.Find("Portrait");
            if(i<2)
            {
                Decor(slot,"PortraitBackdrop","popup_text_background_round18",0,51.5f,165,165);
                slot.Find("PortraitBackdrop").GetComponent<Image>().pixelsPerUnitMultiplier=.6f;
                FitStaffPortraitBottom(portrait,0,51.5f,165,165,135);Active(portrait.Find("Placeholder"),false);
                var image=portrait.GetComponent<Image>();image.color=item!=null&&item.Unlocked?Color.white:new Color(.45f,.40f,.38f,1f);image.preserveAspect=true;
                Place(slot.Find("Name"),0,-85,174,42);Label(slot.Find("Name"),30,false,Brown,item?.Job+"직원");
                Place(slot.Find("RoleLevel"),-62,115,50,50);Label(slot.Find("RoleLevel"),28,false,Cream,item==null?"—":item.BeforeLevel.ToString());
                var roleLevel=slot.Find("RoleLevel").GetComponent<TMP_Text>();roleLevel.alignment=TextAlignmentOptions.Midline;roleLevel.margin=Vector4.zero;
                var roleBounds=TextGlyphYBounds(roleLevel);roleLevel.rectTransform.anchoredPosition-=new Vector2(0,(roleBounds.x+roleBounds.y)*.5f);
                Decor(slot,"LevelBadge","Level_Badge",-62,115,50,50);slot.Find("LevelBadge").SetAsLastSibling();slot.Find("RoleLevel").SetAsLastSibling();
                StaffLock(slot,"StaffLock",0,52,item!=null&&!item.Unlocked);
            }
            else
            {
                var bg=Ensure(slot,"FuturePortrait");Place(bg,0,51.5f,165,165);Picture(bg,"Panel",new Color32(119,98,91,255));
                var silhouette=Ensure(slot,"FutureSilhouette");Place(silhouette,0,51.5f,165,165);
                var silhouetteImage=silhouette.GetComponent<Image>()??silhouette.gameObject.AddComponent<Image>();
                silhouetteImage.sprite=staff!=null&&staff.Length>0?staff[0].Portrait:null;
                silhouetteImage.color=new Color(0,0,0,.2f);silhouetteImage.preserveAspect=true;silhouetteImage.raycastTarget=false;
                FitStaffPortraitBottom(silhouette,0,51.5f,165,165,135);
                StaffLock(slot,"StaffLock",0,52,true);
                var label=Ensure(slot,"Condition");Place(label,0,-87,174,70);
                Label(label,25,false,Cream,"플레이어\n레벨 "+new[]{6,10,15,25}[i-2]);
                // 향후 직원 칸은 같은 잠금 표시를 사용하며 실제 해금·강화 기능은 연결하지 않습니다.
                Picture(slot,"Panel",new Color32(139,119,110,255));
            }
        }
        var detail=root.Find("StaffDetail");Active(detail,true);Place(detail,364,-3,620,710);Picture(detail,"recipebook_background_round35");
        Active(root.Find("LockedStaffDetail"),false);
        foreach(var name in new[]{"Title","Divider","Job","Stats","StatTrack","StatFill","Skill"})Active(detail.Find(name),false);
        Decor(detail,"PortraitBackdrop","popup_text_background_round18",-170,215,210,210);detail.Find("PortraitBackdrop").GetComponent<Image>().pixelsPerUnitMultiplier=18f/35f;
        FitStaffPortraitBottom(detail.Find("Portrait"),-170,215,210,210,175);Active(detail.Find("Portrait/Placeholder"),false);
        var photo=detail.Find("Portrait").GetComponent<Image>();photo.color=Color.white;photo.preserveAspect=true;
        Place(detail.Find("Name"),52.5f,273,165,50);Label(detail.Find("Name"),36);detail.Find("Name").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        Place(detail.Find("Description"),124,206,300,60);Label(detail.Find("Description"),22,true,Dark);detail.Find("Description").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.TopLeft;
        var staffDescription=detail.Find("Description").GetComponent<TMP_Text>();
        var descriptionWords=(view.SelectedStaff?.Description??string.Empty).Split(' ');
        staffDescription.text=string.Join(" ",System.Array.ConvertAll(descriptionWords,word=>"<nobr>"+word+"</nobr>"));
        staffDescription.enableWordWrapping=true;

        Place(detail.Find("UnlockCondition"),124,149,300,50);Label(detail.Find("UnlockCondition"),21,true);detail.Find("UnlockCondition").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        var selected=view.SelectedStaff;
        Decor(detail,"StaffNameTag",selected?.Job=="서빙"?"Staff_Name_Tag_Green":"Staff_Name_Tag_Pink",52.5f,273,165,50);
        detail.Find("Name").SetAsLastSibling();Label(detail.Find("Name"),32,false,Cream,selected?.Job+"직원");detail.Find("Name").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        Decor(detail,"DetailLevelBadge","Level_Badge_Rd19",-227,315,110,40);
        detail.Find("DetailLevelBadge").SetAsLastSibling();
        var detailLevel=Ensure(detail,"DetailLevel");Place(detailLevel,-227,315,110,40);Label(detailLevel,24,false,Cream,"레벨 "+(selected?.BeforeLevel.ToString()??"—"));detailLevel.SetAsLastSibling();
        var change=Ensure(detail,"LevelChange");Place(change,0,42.5f,550,65);Picture(change,"menu_upgrade_change_background_round18");
        bool maximum=selected!=null&&selected.AfterLevel<=selected.BeforeLevel;
        var labelHeading=Ensure(change,"Heading");Place(labelHeading,-145,0,100,60);Label(labelHeading,26,false,Cream,"레벨");Active(labelHeading,!maximum);
        var current=Ensure(change,"Current");Place(current,-20,0,80,60);Label(current,26,true,Cream,selected?.BeforeLevel.ToString()??"—");Active(current,!maximum);
        var arrow=Ensure(change,"Arrow");Place(arrow,65,0,70,60);Label(arrow,36,false,new Color32(159,216,63,255),"››");Active(arrow,!maximum);
        var next=Ensure(change,"Next");Place(next,145,0,90,60);Label(next,26,false,new Color32(159,216,63,255),selected?.AfterLevel.ToString()??"—");Active(next,!maximum);
        Active(change.Find("StatsSummary"),false);
        var maxLabel=Ensure(change,"MaximumLabel");Place(maxLabel,0,0,500,60);Label(maxLabel,24,false,new Color32(255,80,89,255),"최대치 달성!");Active(maxLabel,maximum);
        var maximumText=maxLabel.GetComponent<TMP_Text>();maximumText.alignment=TextAlignmentOptions.Midline;maximumText.margin=Vector4.zero;
        var maximumBounds=TextGlyphYBounds(maximumText);maximumText.rectTransform.anchoredPosition=new Vector2(0,-(maximumBounds.x+maximumBounds.y)*.5f);

        var heading=Ensure(detail,"SkillsHeading");Place(heading,0,-36,550,40);Label(heading,28,false,Brown,"스킬");
        for(int i=0;i<4;i++)
        {
            var skill=Ensure(detail,"SkillSlot"+(i+1));Place(skill,-212+i*144,-132,124,124);Picture(skill,"popup_text_background_round18");skill.GetComponent<Image>().pixelsPerUnitMultiplier=.6f;
            Active(skill.Find("Label"),false);
            bool serving=selected?.Job=="서빙";
            bool locked=i>=2 || selected==null || !selected.Unlocked || i==1&&(!selected.ActiveUnlocked || !(detail.Find("SkillSlot1/SkillGauge").GetComponent<StaffSkillGaugeView>()?.IsFull??false));
            var icon=Ensure(skill,"SkillIcon");Place(icon,0,0,94,94);Picture(icon,(serving?"Icon_Serving_Skill":"Icon_Cooking_Skill")+(i<2?2-i:i+1));FitMenuIconWithMargin(icon,0,0,124,124);Active(icon,!locked&&i<2);
            StaffLock(skill,"Lock",0,0,locked);
            var effect=Ensure(skill,"EffectValue");Place(effect,0,-45,110,30);
            Label(effect,24,false,new Color32(178,246,72,255),selected?.ActiveEffectText??string.Empty);
            Active(effect,i==1&&!locked&&!string.IsNullOrEmpty(selected?.ActiveEffectText));
            var caption=Ensure(detail,"SkillCaption"+i);Place(caption,-212+i*144,-212,140,40);
            Label(caption,20,false,Brown,i==1?(serving?"음료서빙":"조리가속"):(serving?"서빙속도증가":"조리속도증가"));Active(caption,i<2&&!locked);
            var meter=Ensure(skill,"SkillGauge");Place(meter,0,-47,92,14);Picture(meter,"Skill_Gauge_BG").enabled=false;Active(meter,i==0&&!locked);
            Active(meter.Find("Fill"),false);
            if(i==0)
            {
                var gauge=meter.GetComponent<StaffSkillGaugeView>()??meter.gameObject.AddComponent<StaffSkillGaugeView>();
                gauge.Configure(Art("Skill_Gauge_BG")?.texture,Art("Skill_Gauge_Fill")?.texture,icon.GetComponent<Image>(),transform.Find("StaffUpgradeResultPopup")?.gameObject);
                gauge.OnFull=ApplyStaff;
                gauge.Display(selected?.StaffId??-1,selected?.BeforeLevel??1,selected!=null&&selected.ActiveUnlocked);
            }
            if(i==1)
            {
                var reveal=skill.GetComponent<StaffSkillUnlockView>()??skill.gameObject.AddComponent<StaffSkillUnlockView>();
                reveal.Configure(skill.Find("Lock") as RectTransform,icon,effect.gameObject,caption.gameObject);
                reveal.Display(selected?.StaffId??-1,!locked);
            }
            Active(detail.Find("SkillActive"+i),false);
        }
        Decor(detail,"CostBackground","recipebook_price_round18",-118,-278,150,40);
        var coin=Ensure(detail,"CostCoin");Place(coin,-195,-278,65,65);Picture(coin,"Icon_Gold");
        // 공통 골드 스프라이트 이름은 연결된 가격 아이콘에서 그대로 가져옵니다.
        var source=Book.Find("SelectedRecipeDetail/RecipePriceCoin")?.GetComponent<Image>();if(source!=null)coin.GetComponent<Image>().sprite=source.sprite;
        Place(detail.Find("UpgradeCost"),-110,-278,134,40);Label(detail.Find("UpgradeCost"),30);
        Button(detail.Find("UpgradeButton"),127,-276,250,80,"강화");
        var upgradeLabel=detail.Find("UpgradeButton/Label").GetComponent<TMP_Text>();upgradeLabel.fontSize=40;upgradeLabel.alignment=TextAlignmentOptions.Midline;upgradeLabel.margin=Vector4.zero;
        var upgradeBounds=TextGlyphYBounds(upgradeLabel);upgradeLabel.rectTransform.anchoredPosition=new Vector2(0,-(upgradeBounds.x+upgradeBounds.y)*.5f);
        bool can=selected!=null&&selected.Unlocked&&selected.CanUpgrade&&view.CanManage;
        var button=detail.Find("UpgradeButton").GetComponent<UnityEngine.UI.Button>();button.interactable=can;button.transition=Selectable.Transition.None;
        if(!can){Picture(button.transform,"Button_Disabled");Active(detail.Find("UpgradeButtonShadow"),false);}
        if(can){detail.Find("UpgradeButton").GetComponent<Image>().color=new Color32(255,80,89,255);detail.Find("UpgradeButtonShadow").GetComponent<Image>().color=new Color32(164,44,56,255);}
        ApplyBookmarks(3);
    }

    /// <summary>직원 및 스킬 잠금에 UI팀의 공통 자물쇠 리소스를 사용합니다.</summary>
    void StaffLock(Transform parent,string name,float x,float y,bool visible)
    {
        var root=Ensure(parent,name);Place(root,x,y,50,56);Active(root,visible);
        Picture(root,"Icon_Lock");root.GetComponent<Image>().preserveAspect=true;
        // 이전 도형 잠금의 자식은 참조를 유지하며 표시만 끕니다.
        Active(root.Find("Shackle"),false);Active(root.Find("Body"),false);
        root.SetAsLastSibling();
    }

    /// <summary>책갈피 안쪽 끝을 고정하고 기존 기울기를 따라 늘립니다.</summary>
    public void ApplyBookmarks(int selected)
    {
        string[] tabs={"TodayTab","UpgradeTab","RecipesTab","StaffManagementBookmark"};
        string[] backgrounds={"Index_Todays","Index_Upgradee","Index_Drink","Index_Staff"};
        string[] icons={"Icon_Today","Icon_Upgrade","Icon_Drink","Icon_Staff"};
        float[] ys={240,100,-40,-300};
        // 원본 PNG의 중심선 기울기입니다. 화면 비율에 따른 기울기와 부모 회전을 함께 반영합니다.
        float[] artworkSlopes={-.048176f,-.137581f,.096277f,-.137581f};
        // 아이콘 원화의 수평축 기울기(화살표 양 날개, 컵 밑면, 직원 어깨선).
        // 이미지의 비균일 확대까지 반영해 책갈피와 동일한 화면 수평축으로 보정합니다.
        float[] iconArtworkSlopes={0f,.104167f,-.033333f,.098592f};
        var cover=Book.Find("BookCover");
        var front=Book.Find("BookFront");
        if(cover!=null)cover.SetAsFirstSibling();
        int layer=1;
        for(int i=0;i<tabs.Length;i++){
            var tab=Book.Find(tabs[i]);if(tab==null)continue;
            var motion=tab.GetComponent<BookBookmarkMotionView>();
            if(motion==null)motion=tab.gameObject.AddComponent<BookBookmarkMotionView>();
            bool active=i==selected;
            Place(tab,780,ys[i],96,96);
            tab.localRotation=Quaternion.identity;
            Picture(tab,backgrounds[i]);
            foreach(Transform child in tab)if(child.name!="BookmarkIcon")Active(child,false);
            var icon=Ensure(tab,"BookmarkIcon");Place(icon,0,0,74,74);
            Picture(icon,icons[i]);
            var sprite=tab.GetComponent<Image>().sprite;var rect=new Rect(0,0,150,96);
            float artAngle=sprite==null?0f:Mathf.Atan2(-artworkSlopes[i]*rect.height/sprite.rect.height,rect.width/sprite.rect.width)*Mathf.Rad2Deg;
            var iconSprite=icon.GetComponent<Image>().sprite;var iconRect=iconSprite==null?new Rect(0,0,74,74):iconSprite.rect;
            float iconArtAngle=iconSprite==null?0f:Mathf.Atan2(-iconArtworkSlopes[i]*iconRect.height/iconSprite.rect.height,iconRect.width/iconSprite.rect.width)*Mathf.Rad2Deg;
            icon.localRotation=Quaternion.Euler(0,0,artAngle-iconArtAngle);icon.SetAsLastSibling();
            tab.SetSiblingIndex(layer++);
            var rest=new Vector2(780,ys[i]);
            var direction=new Vector2(Mathf.Cos(artAngle*Mathf.Deg2Rad),Mathf.Sin(artAngle*Mathf.Deg2Rad));
            motion.Animate(active,rest,direction);
        }
        // Page art covers the inner ends of the tabs without blocking page controls.
        if(front!=null){front.SetSiblingIndex(layer);front.GetComponent<Image>().raycastTarget=false;}
    }
    // 요리 원본의 투명 여백을 제외한 픽셀 영역입니다. 원본 이미지는 변경하지 않습니다.
    static readonly System.Collections.Generic.Dictionary<string,Rect> MenuIconPixelBounds=new()
    {
        ["BeerMug"]=new Rect(45,21,147,219),
        ["BlueTang_Grilled"]=new Rect(67,57,120,143),
        ["BlueTang_Gunkan"]=new Rect(64,81,128,99),
        ["BlueTangNigiri"]=new Rect(61,88,131,83),
        ["CucumberPlate"]=new Rect(32,69,187,106),
        ["EightbandedButterflyfish_Grilled"]=new Rect(58,58,139,142),
        ["EightbandedButterflyfish_Nigiri"]=new Rect(54,74,148,106),
        ["FoodLeftovers"]=new Rect(8,77,237,120),
        ["GrapefruitAde_Icon"]=new Rect(82,40,90,183),
        ["GreenGrapeAde_Icon"]=new Rect(82,40,87,183),
        ["IndusRiverDolphin_Grilled"]=new Rect(67,59,123,142),
        ["IndusRiverDolphin_Gunkan"]=new Rect(64,69,128,116),
        ["IndusRiverDolphin_Nigiri"]=new Rect(63,79,143,96),
        ["LemonAde_Icon"]=new Rect(82,40,90,183),
        ["MantaRay_Fry"]=new Rect(26,60,212,144),
        ["MantaRay_Nigiri"]=new Rect(31,67,195,121),
        ["RedSeaHorse_Grilled"]=new Rect(63,45,130,169),
        ["RedSeahorse_Nigiri"]=new Rect(54,77,148,100),
        ["SkunkTileFish_Grilled"]=new Rect(60,59,134,136),
        ["SkunkTileFish_Gunkan"]=new Rect(66,77,124,103),
        ["SkunkTilefish_Nigiri"]=new Rect(56,88,151,85),
        ["SkunkTileFish_Roll"]=new Rect(75,68,106,115),
        ["SoySauce"]=new Rect(63,25,130,211),
        ["Rice"]=new Rect(42,44,169,173),
        ["BlueTang"]=new Rect(57,86,160,88),
        ["SeaWead"]=new Rect(20,39,219,178),
        ["Salt"]=new Rect(31,19,197,219),
        ["Icon_Cooking_Skill1"]=new Rect(0,0,85,78),
        ["Icon_Cooking_Skill2"]=new Rect(0,0,83,78),
        ["Icon_Serving_Skill1"]=new Rect(8,7,87,98),
        ["Icon_Serving_Skill2"]=new Rect(0,0,94,56),
        ["ServingNPC"]=new Rect(69,28,119,228),
        ["Cooker"]=new Rect(61,48,135,208),
        ["Tea"]=new Rect(54,37,148,173),
        ["Watanabe_Grilled"]=new Rect(65,58,124,141),
        ["Watanabe_Nigiri"]=new Rect(57,81,142,96),
        ["WhaleShark_Fry"]=new Rect(23,55,205,163),
        ["WhaleShark_Grilled"]=new Rect(19,84,215,120),
        ["WhaleShark_Nigiri"]=new Rect(45,67,166,119),
        ["WhaleShark_Stake"]=new Rect(39,78,186,118),
    };
    public static void FitTodayMenuIcon(Transform target,float x,float y,float visibleSize=158f)
    {
        var image=target.GetComponent<Image>();var sprite=image.sprite;
        if(sprite==null || !MenuIconPixelBounds.TryGetValue(sprite.name,out var pixels)){Place(target,x,y,visibleSize,visibleSize);return;}
        float scale=visibleSize/Mathf.Max(pixels.width,pixels.height);
        Place(target,x+(sprite.rect.width*.5f-pixels.center.x)*scale,y+(pixels.center.y-sprite.rect.height*.5f)*scale,sprite.rect.width*scale,sprite.rect.height*scale);
        image.preserveAspect=true;
    }
    // 투명 여백을 제외한 아이템을 프레임 안쪽 70% 영역에 비율을 유지해 배치합니다.
    static void FitMenuIconWithMargin(Transform target,float x,float y,float width,float height)
    {
        var image=target.GetComponent<Image>();var sprite=image.sprite;
        if(sprite==null){Place(target,x,y,width*.7f,height*.7f);return;}
        if(!MenuIconPixelBounds.TryGetValue(sprite.name,out var pixels))pixels=new Rect(0,0,sprite.rect.width,sprite.rect.height);
        float scale=Mathf.Min(width*.7f/pixels.width,height*.7f/pixels.height);
        Place(target,x+(sprite.rect.width*.5f-pixels.center.x)*scale,y+(pixels.center.y-sprite.rect.height*.5f)*scale,sprite.rect.width*scale,sprite.rect.height*scale);
        image.preserveAspect=true;
    }
    // 가이드의 사진칸 안에서 캐릭터 크기를 맞추고 아래쪽을 빈틈 없이 붙입니다.
    static void FitStaffPortraitBottom(Transform target, float x, float y, float width, float height, float portraitHeight = 0f)
    {
        var image = target.GetComponent<Image>();
        float visibleHeight = portraitHeight > 0f ? portraitHeight : height * (5f / 6f);
        var size = image.sprite != null ? image.sprite.rect.size : new Vector2(width, height);
        float scale = Mathf.Min(width / size.x, visibleHeight / size.y);
        var fitted = size * scale;
        Place(target, x, y - height * .5f + fitted.y * .5f, fitted.x, fitted.y);
        image.preserveAspect = true;
    }
    static Vector2 TextGlyphYBounds(TMP_Text text)
    {
        text.ForceMeshUpdate(true,true);
        float low=float.PositiveInfinity,high=float.NegativeInfinity;
        for(int i=0;i<text.textInfo.characterCount;i++)
        {
            var c=text.textInfo.characterInfo[i];if(!c.isVisible||c.textElement==null)continue;
            var metrics=c.textElement.glyph.metrics;
            high=Mathf.Max(high,c.baseLine+metrics.horizontalBearingY*c.scale);
            low=Mathf.Min(low,c.baseLine+(metrics.horizontalBearingY-metrics.height)*c.scale);
        }
        return float.IsInfinity(low)?Vector2.zero:new Vector2(low,high);
    }
    void TodayCards()
    {
        var today=Book.Find("TodayMenuList"); Place(today,-365,-45,620,640);
        var notice=today.Find("EndOfServiceNotice"); Place(notice,0,325,620,40); Label(notice,22,true,Dark);
        var slots=today.GetComponentsInChildren<SushiTodayMenuSlotView>(true);
        for(int i=0;i<slots.Length;i++)
        {
            var t=slots[i].transform; Place(t,-209+(i%3)*210,155-(i/3)*310,190,290); Picture(t,"todays_menu_slot");
            var c=t.Find("RegisteredContent"); Place(c,0,0,190,290);
            // 카드 원본에 포함된 배경과 겹치지 않도록 별도 배경은 숨깁니다.
            Active(c.Find("IconBackground"),false);
            FitMenuIconWithMargin(c.Find("FoodIcon"),0,55,150,146); Place(c.Find("DishName"),0,-51,180,56); Label(c.Find("DishName"),28);
            var name=c.Find("DishName").GetComponent<TMP_Text>();name.enableAutoSizing=true;name.fontSizeMin=18;name.fontSizeMax=28;name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
            Place(c.Find("Info"),0,-100,180,45); Label(c.Find("Info"),20);
            var presentation=GetComponent<IjakayaView>().MenuView.CurrentPresentation;
            var menu=presentation?.Menus!=null && i<presentation.Menus.Length?presentation.Menus[i]:null;
            UISelectionStrokeView.SetSelected(t, menu != null && menu.Registered && (i==0 || GetComponent<IjakayaView>().SelectedMenuSlotIndex == i), 35);
            string salePrice=null;
            if(menu!=null && menu.Registered){
                var recipe=presentation.Recipes==null?null:System.Array.Find(presentation.Recipes,entry=>entry!=null && entry.RecipeId==menu.RecipeId);
                salePrice=recipe?.SalePrice;
                if(i==0 && string.IsNullOrEmpty(salePrice))salePrice=GetComponentInChildren<SushiMenuBookView>(true)?.BasicFoodPrice;
            }
            bool hasPrice=!string.IsNullOrEmpty(salePrice);
            if(hasPrice){Place(c.Find("Info"),30,-100,80,42);Label(c.Find("Info"),28,false,Brown,salePrice);Decor(c,"PriceBadge","recipebook_price_round18",15,-100,120,42);Decor(c,"Coin","Coin_Icon",-40,-100,50,50);c.Find("Coin").SetAsLastSibling();c.Find("Info").SetAsLastSibling();}
            Active(c.Find("PriceBadge"),hasPrice);Active(c.Find("Coin"),hasPrice);
            var badge=Ensure(c,"FixedMenuBadge");Place(badge,0,135,110,50);Picture(badge,"recipe_lable_green");
            var badgeLabel=Ensure(badge,"Label");Place(badgeLabel,0,0,110,50);Label(badgeLabel,26,false,Cream,"고정");badge.SetAsLastSibling();Active(badge,i==0 && menu!=null && menu.Registered);
            var manage=c.Find("ManageButton"); Place(manage,0,0,190,290); manage.GetComponent<Image>().color=Color.clear; Active(manage.Find("Label"),false);
            var add=t.Find("AddMenuButton"); Place(add,0,0,190,290); Picture(add,"todays_menu_slot"); Active(add.Find("EmptyDishIcon"),false);
            Place(add.Find("Label"),0,0,180,180); Label(add.Find("Label"),28,false,Dark,"+\n메뉴\n추가하기");
            ApplyTodayEmptyCard(add,menu!=null&&!menu.Registered&&!menu.Interactable,menu?.Information);
        }
        // 글자의 보이는 하단과 첫 줄 슬롯 상단 사이의 중앙에 안내 글자 중심을 맞춥니다.
        var noticeText=notice.GetComponent<TMP_Text>();noticeText.alignment=TextAlignmentOptions.Midline;
        var todayTitle=Book.Find("BookTitle").GetComponent<TMP_Text>();
        if(slots.Length>0 && todayTitle.text=="오늘의 메뉴")
        {
            var titleInk=TextGlyphYBounds(todayTitle);
            float titleBottom=Book.InverseTransformPoint(todayTitle.transform.TransformPoint(new Vector3(0,titleInk.x,0))).y;
            var firstSlot=(RectTransform)slots[0].transform;
            float slotTop=Book.InverseTransformPoint(firstSlot.TransformPoint(new Vector3(0,firstSlot.rect.yMax,0))).y;
            var middle=Book.TransformPoint(new Vector3(0,(titleBottom+slotTop)*.5f,0));
            var localMiddle=notice.parent.InverseTransformPoint(middle);
            var noticeInk=TextGlyphYBounds(noticeText);
            var noticeRect=(RectTransform)notice;noticeRect.anchoredPosition=new Vector2(0,localMiddle.y-(noticeInk.x+noticeInk.y)*.5f);
        }
        var footer=today.Find("AddMenuFooter"); Place(footer,211,-155,190,290); Picture(footer,"todays_menu_slot");
        Place(footer.Find("Label"),0,0,180,180); Label(footer.Find("Label"),28,false,Dark,"+\n메뉴\n추가하기");
        ApplyTodayEmptyCard(footer,false,null);Active(footer,slots.Length<6);
    }

    /// <summary>등록 카드와 같은 위쪽 아이콘 칸과 아래쪽 안내 구조로 빈 칸을 표시합니다.</summary>
    public void ApplyTodayEmptyCard(Transform button,bool locked,string information)
    {
        if(button==null)return;
        var image=Picture(button,"todays_menu_slot");
        image.color=locked?new Color(.66f,.67f,.71f,1f):Color.white;
        Active(button.Find("EmptyDishIcon"),false);
        var plus=Ensure(button,"AddMark");Place(plus,0,55,146,146);Label(plus,48,false,Cream,"+");
        plus.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Midline;Active(plus,!locked);
        var padlock=Ensure(button,"LockIcon");Place(padlock,0,55,46,54);Picture(padlock,"Icon_Lock");Active(padlock,locked);
        var label=Ensure(button,"Label");Place(label,0,-88,180,84);
        Label(label,28,false,locked?Cream:Dark,locked?(string.IsNullOrWhiteSpace(information)?"잠김":information):"메뉴\n추가하기");
        label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Midline;Active(label,true);
    }
    /// <summary>레시피 선택은 제공받은 흰색 외곽선 카드 스프라이트로 표시합니다.</summary>
    public void SetRecipeCardSelected(UnityEngine.UI.Button button,bool selected)
    {
        Picture(button.transform,"todays_menu_slot");
        UISelectionStrokeView.SetSelected(button.transform, selected, 35);
        var outline=button.GetComponent<UnityEngine.UI.Outline>();if(outline!=null)outline.enabled=false;
    }
    void BasicCards()
    {
        var basic=Book.Find("BasicMenuSummary"); Place(basic,-365,-45,632,640); Active(basic.Find("Heading"),false);
        var scroll=basic.GetComponentInChildren<ScrollRect>(true); Place(scroll.transform,0,0,632,620); Place(scroll.viewport,0,0,632,620);
        var mask=scroll.viewport.GetComponent<RectMask2D>();
        if(mask!=null)mask.padding=new Vector4(0,0,0,-12);
        var grid=scroll.content.GetComponent<GridLayoutGroup>();
        if(grid==null && scroll.content.GetComponent<VerticalLayoutGroup>()==null)grid=scroll.content.gameObject.AddComponent<GridLayoutGroup>();
        if(grid!=null){grid.cellSize=new Vector2(190,290);grid.spacing=new Vector2(20,20);grid.padding=new RectOffset(12,10,6,6);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.childAlignment=TextAnchor.UpperLeft;}
        var fitter=scroll.content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        ApplyRecipeCards();
        var d=Book.Find("BasicMenuDetail"); Place(d,365,-12,650,712); d.GetComponent<Image>().color=Color.clear;
        var back=d.Find("PreviewBackground"); Place(back,0,150,900,700);Picture(back,"Basic_Menu_Right_Gradient"); back.GetComponent<Image>().enabled=true; Active(back,true);
        FitBasicRecipePreview();
        Place(d.Find("SelectedBasicName"),0,-60,610,52);Label(d.Find("SelectedBasicName"),38);
        Place(d.Find("PriceCoin"),-40,-133,58,58);Place(d.Find("Price"),30,-133,90,42);Label(d.Find("Price"),30);
        Decor(d,"PriceBadge","recipebook_price_round18",15,-133,120,42);
        Place(d.Find("Description"),0,-250,480,128);Label(d.Find("Description"),23,true,Dark);
        var description=d.Find("Description").GetComponent<TMP_Text>();description.alignment=TextAlignmentOptions.Top;description.enableWordWrapping=true;
        description.enableAutoSizing=true;description.fontSizeMin=22;description.fontSizeMax=23;
    }
    /// <summary>우측 레시피 그림이 아래 이름·가격 영역을 침범하지 않도록 비율과 표시 상한을 맞춥니다.</summary>
    public void FitBasicRecipePreview()
    {
        var target = Book?.Find("BasicMenuDetail/PreviewBackground/PreviewIcon");
        if (target == null) return;
        var image = target.GetComponent<Image>();
        var sprite = image.sprite;
        if (sprite == null) { Place(target, 5, 1, 420, 294); return; }
        if (!MenuIconPixelBounds.TryGetValue(sprite.name, out var pixels))
            pixels = new Rect(0, 0, sprite.rect.width, sprite.rect.height);
        // 기존 크기에 들어오는 그림은 유지하고 긴 이미지의 보이는 영역만 제한합니다.
        float scale = Mathf.Min(512f / Mathf.Max(sprite.rect.width, sprite.rect.height),
            420f / pixels.width, 294f / pixels.height);
        Place(target, 5 + (sprite.rect.width * .5f - pixels.center.x) * scale,
            1 + (pixels.center.y - sprite.rect.height * .5f) * scale,
            sprite.rect.width * scale, sprite.rect.height * scale);
        image.preserveAspect = true;
    }
    /// <summary>실제 데이터로 늘어난 카드에도 동일한 크기와 간격을 적용합니다.</summary>
    public void ApplyRecipeCards()
    {
        var basic=Book.Find("BasicMenuSummary");if(basic==null)return;
        var scroll=basic.GetComponentInChildren<ScrollRect>(true);if(scroll==null)return;
        int index=0;
        foreach(Transform card in scroll.content)
        {
            if(card.GetComponent<UnityEngine.UI.Button>()==null)continue;
            Picture(card,"todays_menu_slot");
            // 상단 아이템 프레임(150×146)의 각 변에 최소 15% 여백을 둡니다.
            foreach(var icon in card.GetComponentsInChildren<Image>(true))
                if(icon.transform.parent==card && icon.name.Contains("Icon"))FitTodayMenuIcon(icon.transform,0,55,146f*.70f);
            Active(card.Find("DishPreviewBackground"),false);
            foreach(var text in card.GetComponentsInChildren<TMP_Text>(true))if(text.transform.parent==card && text.name!="Price" && text.name!="Kind"){Place(text.transform,0,-51,180,56);Label(text.transform,28);text.enableWordWrapping=true;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=28;text.overflowMode=TextOverflowModes.Ellipsis;}
            Active(card.Find("Kind"),false);Place(card.Find("Coin"),-40,-100,50,50);Place(card.Find("Price"),30,-100,80,42);Label(card.Find("Price"),28);
            Decor(card,"PriceBadge","recipebook_price_round18",15,-100,120,42);
            var badge=Ensure(card,"FixedMenuBadge");Place(badge,0,135,110,50);var badgeImage=Picture(badge,"Panel",new Color32(240,181,0,255));badgeImage.sprite=null;
            var label=Ensure(badge,"Label");Place(label,0,0,110,50);Label(label,26,false,Cream,"고정");
            badge.SetAsLastSibling();Active(badge,index==0);index++;
        }
    }
    /// <summary>등록 메뉴 관리창의 기존 버튼 연결을 유지하고 최신 구상도로 배치합니다.</summary>
    public void ApplyRegisteredMenuActions()
    {
        var root=transform.Find("RegisteredMenuActionsPopup");if(root==null)return;
        root.GetComponent<Image>().color=new Color(0,0,0,.6f);
        var p=root.Find("Panel");Place(p,0,0,650,580);Picture(p,"Panel",new Color32(244,212,190,255)).pixelsPerUnitMultiplier=.44f;
        Active(p.Find("Divider"),false);
        Place(p.Find("Title"),1.5f,229,500,60);Label(p.Find("Title"),36,false,Brown,"등록 메뉴 관리");
        var popupTitle=p.Find("Title").GetComponent<TMP_Text>();popupTitle.alignment=TextAlignmentOptions.Midline;popupTitle.margin=Vector4.zero;
        var close=p.Find("BackButton");Place(close,277,235,40,40);Picture(close,"popup_x_button");Active(close.Find("Label"),false);
        var preview=Ensure(p,"SelectedFoodBackdrop");Place(preview,-158.5f,95,165,150);Picture(preview,"popup_text_background_round18").pixelsPerUnitMultiplier=.6f;
        var icon=Ensure(p,"SelectedFoodIcon");if(icon.GetComponent<Image>()==null)icon.gameObject.AddComponent<Image>();FitMenuIconWithMargin(icon,-158.5f,95,165,150);icon.GetComponent<Image>().raycastTarget=false;icon.GetComponent<Image>().enabled=icon.GetComponent<Image>().sprite!=null;
        Place(p.Find("SelectedDish"),120,122,320,60);Label(p.Find("SelectedDish"),38,false,Brown);
        var dish=p.Find("SelectedDish").GetComponent<TMP_Text>();dish.alignment=TextAlignmentOptions.MidlineLeft;dish.enableAutoSizing=true;dish.fontSizeMin=22;dish.fontSizeMax=38;dish.enableWordWrapping=true;
        var quantity=Ensure(p,"RegisteredQuantity");Place(quantity,11,63,100,40);Label(quantity,26,false,Cream);var text=quantity.GetComponent<TMP_Text>();text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=26;
        Decor(p,"QuantityBadge","Panel",11,63,100,40,new Color32(115,78,74,255));quantity.SetAsLastSibling();
        Decor(p,"ChoicesBackground","Panel",0,-138,480,220,new Color32(166,120,107,255));p.Find("ChoicesBackground").GetComponent<Image>().pixelsPerUnitMultiplier=.7f;
        string[] names={"ReplaceButton","QuantityButton","DeleteButton"};string[] labels={"요리 교체","수량 변경","요리 삭제"};
        for(int i=0;i<names.Length;i++){
            var button=p.Find(names[i]);Place(button,0,-78-i*62,430,50);Picture(button,"Panel",i==0?new Color32(249,234,216,255):new Color32(193,150,131,255));
            Place(button.Find("Label"),0,0,430,50);Label(button.Find("Label"),30,false,i==0?Brown:Cream,labels[i]);
        }
        close.SetAsLastSibling();
    }
    /// <summary>완료 결과만 표시하며 추가 해금 행 유무에 따라 팝업과 배경 높이를 함께 조절합니다.</summary>
    public void ApplyUpgradeComplete(SushiRecipeUpgradeResultView view,SushiRecipeUpgradeResultView.ResultData data)
    {
        if(view==null||view.PageRoot==null||data==null)return;
        var p=view.PageRoot.transform.Find("Panel");if(p==null)return;
        bool unlock=data.IsStaff&&data.ShowUnlock;
        float panelHeight=unlock?736f:652f;
        float half=panelHeight*.5f;
        Color reachedLevel=new Color32(159,216,63,255);
        Place(p,0,0,730,panelHeight);Picture(p,"Panel",new Color32(244,212,191,255)).pixelsPerUnitMultiplier=.44f;
        Active(p.Find("Divider"),false);Active(p.Find("UpgradedStats"),false);
        var title=p.Find("Title");Active(title,true);Place(title,0,half-70,620,60);Label(title,40,false,Brown,data.IsStaff?"직원 강화 완료":"요리 강화 완료");
        title.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Midline;
        Decor(p,"ResultPortraitBackground","popup_text_background_round18",-160,half-195,165,150);
        var icon=p.Find("FoodIcon");Active(icon,data.Icon!=null);IjakayaView.SetDisplayIcon(icon.GetComponent<Image>(),data.Icon);
        if(data.IsStaff)
        {
            // 직원 썸네일은 가이드처럼 비율을 유지하고 사진칸 하단에 붙입니다.
            FitStaffPortraitBottom(icon,-160,half-195,165,150);
        }
        else FitMenuIconWithMargin(icon,-160,half-195,165,150);
        var name=p.Find("CompleteMessage");Place(name,130,half-173,340,72);Label(name,34,false,Brown,data.Name??string.Empty);
        var nameText=name.GetComponent<TMP_Text>();nameText.alignment=TextAlignmentOptions.MidlineLeft;nameText.enableAutoSizing=true;nameText.fontSizeMin=22;nameText.fontSizeMax=34;nameText.enableWordWrapping=true;nameText.overflowMode=TextOverflowModes.Ellipsis;
        var badge=Ensure(p,"ResultLevelBadge");Place(badge,15,half-227,110,40);Picture(badge,"Level_Badge_Rd19");Active(badge,data.Level>0);
        var level=Ensure(p,"ResultLevel");Place(level,15,half-227,110,40);Label(level,24,false,Cream,"레벨 "+data.Level);level.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Midline;Active(level,data.Level>0);
        // 이전/도달 레벨과 실제 변경 수치를 두 영역으로 분리합니다.
        var levelRow=Ensure(p,"ResultLevelChangeBackground");Place(levelRow,0,half-343,480,62);
        Picture(levelRow,"Panel",new Color32(115,78,74,255)).pixelsPerUnitMultiplier=1.5f;levelRow.SetAsFirstSibling();
        int previousLevel=data.BeforeLevel>0?data.BeforeLevel:Mathf.Max(0,data.Level-1);
        foreach(var spec in new[]{("ResultLevelHeading",-115f,"레벨"),("ResultBeforeLevel",0f,previousLevel.ToString()),("ResultLevelArrow",70f,"››"),("ResultAfterLevel",140f,data.Level.ToString())})
        {
            var t=Ensure(p,spec.Item1);Place(t,spec.Item2,half-343,spec.Item1=="ResultLevelHeading"?130:65,50);
            Label(t,26,false,spec.Item1=="ResultAfterLevel"||spec.Item1=="ResultLevelArrow"?reachedLevel:Cream,spec.Item3);
            t.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Midline;Active(t,data.Level>0);
        }
        Active(levelRow,data.Level>0);
        float changeHeight=unlock?164:80;
        var changeBackground=Ensure(p,"ResultChangeBackground");Place(changeBackground,0,half-387-changeHeight*.5f,480,changeHeight);
        Picture(changeBackground,"Panel",Dark).pixelsPerUnitMultiplier=1.5f;changeBackground.SetAsFirstSibling();
        float row=half-427;
        bool compare=!string.IsNullOrEmpty(data.BeforeValue);
        var before=Ensure(p,"ResultBefore");var after=Ensure(p,"ResultAfter");var arrow=Ensure(p,"ResultArrow");
        var coinBefore=Ensure(p,"ResultBeforeCoin");var coinAfter=Ensure(p,"ResultAfterCoin");
        var statIcon=Ensure(p,"ResultStatIcon");var statName=Ensure(p,"ResultStatName");
        Active(statIcon,data.IsStaff&&compare);Active(statName,data.IsStaff&&compare);
        Active(coinBefore,!data.IsStaff&&compare);Active(coinAfter,!data.IsStaff&&compare);
        if(data.IsStaff&&compare)
        {
            Place(statIcon,-182,row,52,52);var si=Picture(statIcon,null);si.sprite=data.StatIcon!=null?data.StatIcon:Art(data.StatLabel=="이동 속도"?"Icon_Serving_Skill2":"Icon_Cooking_Skill2");si.preserveAspect=true;
            Place(statName,-70,row,160,50);Label(statName,24,false,Cream,data.StatLabel??string.Empty);
            Place(before,80,row,70,50);Place(arrow,133,row,36,50);Place(after,185,row,70,50);
        }
        else
        {
            Place(coinBefore,-178,row,48,48);Picture(coinBefore,"Coin_Icon");Place(coinAfter,100,row,48,48);Picture(coinAfter,"Coin_Icon");
            Place(before,-106,row,95,50);Place(arrow,0,row,70,50);Place(after,compare?170:0,row,compare?95:450,50);
        }
        Label(before,30,false,Cream,data.BeforeValue??string.Empty);Label(after,30,false,Cream,data.AfterValue??string.Empty);Label(arrow,34,false,reachedLevel,"››");
        foreach(var t in new[]{before,after}){var tx=t.GetComponent<TMP_Text>();tx.alignment=TextAlignmentOptions.Midline;tx.enableAutoSizing=true;tx.fontSizeMin=18;tx.fontSizeMax=30;tx.enableWordWrapping=false;}
        Active(before,compare);Active(arrow,compare);
        var unlockIcon=Ensure(p,"ResultUnlockIcon");Place(unlockIcon,-182,half-507,52,52);var ui=Picture(unlockIcon,null);ui.sprite=data.UnlockIcon!=null?data.UnlockIcon:Art(data.StatLabel=="이동 속도"?"Icon_Serving_Skill1":"Icon_Cooking_Skill1");ui.preserveAspect=true;Active(unlockIcon,unlock);
        var unlockText=Ensure(p,"ResultUnlockMessage");Place(unlockText,30,half-507,330,50);Label(unlockText,26,false,Cream,data.UnlockMessage??string.Empty);unlockText.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;Active(unlockText,unlock);
        var button=p.Find("ConfirmButton");Button(button,0,-half+88,248,80,"확인");
        Place(button.Find("Label"),0,1.5f,248,80);var confirm=button.Find("Label").GetComponent<TMP_Text>();confirm.alignment=TextAlignmentOptions.Midline;confirm.margin=Vector4.zero;
    }

    /// <summary>직원 강화의 실제 데이터와 버튼 연결을 유지하고 730×450 안내창으로 표시합니다.</summary>
    public void ApplyStaffUpgradePopup()
    {
        var root=transform.Find("StaffUpgrade");if(root==null)return;
        root.GetComponent<Image>().color=new Color(0,0,0,.6f);
        var p=root.Find("Panel");Place(p,0,0,730,450);
        Picture(p,"Panel",new Color32(244,212,191,255)).pixelsPerUnitMultiplier=.44f;
        foreach(var name in new[]{"Divider","Portrait","Name","Before","After","Arrow","Perk","Status"})Active(p.Find(name),false);
        Place(p.Find("Title"),0,160,500,60);Label(p.Find("Title"),43,false,Brown,"안내");
        var prompt=p.Find("Prompt");Place(prompt,0,36,650,50);Label(prompt,30,false,Brown,"재화를 소모해 강화하시겠습니까?");
        var close=p.Find("CloseButton");Place(close,316,162,40,40);Picture(close,"popup_x_button");Active(close.Find("Label"),false);
        var heading=Ensure(p,"GoldHeading");Place(heading,-134,-90,220,40);Label(heading,24,false,Brown,"소모 재화");
        Decor(p,"CostBackground","recipebook_price_round18",-130,-140,170,48);
        var coin=Ensure(p,"CostCoin");Place(coin,-210,-140,75,75);Picture(coin,"Coin_Icon");
        var source=Book?.Find("SelectedRecipeDetail/RecipePriceCoin")?.GetComponent<Image>();if(source!=null)coin.GetComponent<Image>().sprite=source.sprite;
        Place(p.Find("Cost"),-105,-140,120,48);Label(p.Find("Cost"),32);p.Find("Cost").SetAsLastSibling();
        Button(p.Find("ConfirmButton"),145,-127,248,80,"확인");Label(p.Find("ConfirmButton/Label"),42,false,Cream);
        Place(p.Find("ConfirmButton/Label"),0,1.5f,248,80);
        var confirmLabel=p.Find("ConfirmButton/Label").GetComponent<TMP_Text>();confirmLabel.alignment=TextAlignmentOptions.Midline;confirmLabel.margin=Vector4.zero;
        close.SetAsLastSibling();
    }

    /// <summary>등록·강화 버튼 연결을 유지하면서 안내창의 표시만 정리합니다.</summary>
    public void ApplyPopups()
    {
        ApplyRegisteredMenuActions();
        ApplyStaffUpgradePopup();
        foreach(bool upgrade in new[]{false,true})
        {
            var root=transform.Find(upgrade?"RecipeUpgradeConfirmPopup":"MenuRegistrationPopup");
            // 안내창 배경은 구상도의 짙은 푸른색 감광을 Linear 색 공간에서 맞춥니다.
            root.GetComponent<Image>().color=new Color(0,0,0,.6f);
            var p=root.Find("Panel"); Place(p,0,0,730,upgrade?630:840); Picture(p,"Panel",new Color32(244,212,191,255));
            // 원본 22px 모서리를 50px 반경으로 표시합니다. 공유 스프라이트 자체는 변경하지 않습니다.
            p.GetComponent<Image>().pixelsPerUnitMultiplier=.44f;
            foreach(Transform t in p)if(t.name=="Divider" || t.name.StartsWith("EmptyIngredient") || t.name=="Before" || t.name=="After" || t.name=="Arrow")Active(t,false);
            Place(p.Find("Title"),upgrade?1:0,upgrade?253:360,500,upgrade?60:70);Label(p.Find("Title"),upgrade?36:42,false,Brown,"안내");
            var close=p.Find(upgrade?"CloseButton":"CancelButton");Place(close,313,upgrade?252:362,40,40);Picture(close,"popup_x_button");Active(close.Find("Label"),false);
            Button(p.Find(upgrade?"UpgradeButton":"ConfirmButton"),133,upgrade?-220:-210,248,80,"확인");
            if(upgrade)
            {
                var prompt=p.Find("Prompt"); Place(prompt,1,155,630,60);Label(prompt,30,false,Brown,"재화와 재료를 소모해 강화하시겠습니까?");
                Active(p.Find("PromptBackground"),false);
                Place(p.Find("ConsumptionNotice"),0,60,500,50);Label(p.Find("ConsumptionNotice"),28,false,Brown,"소모 재료");
                var slots=p.GetComponentsInChildren<SushiIngredientSlotView>(true);
                // 확인창은 원본 갈색 리소스를 사용하고 모서리를 재료 슬롯에 맞춥니다.
                for(int i=0;i<4;i++){Decor(p,"MaterialPlaceholder"+i,"popup_text_background_round18",-210+i*140,-35,124,124);p.Find("MaterialPlaceholder"+i).GetComponent<Image>().pixelsPerUnitMultiplier=.6f;}
                for(int i=0;i<slots.Length;i++){Ingredient(slots[i].transform,-210+i*140,-35,124);Picture(slots[i].transform,"popup_text_background_round18").pixelsPerUnitMultiplier=.6f;}
                var label=Ensure(p,"GoldHeading");Place(label,-145,-178,220,45);Label(label,28,false,Brown,"소모 재화");
                Place(p.Find("GoldCost"),-120,-234,150,42);Label(p.Find("GoldCost"),30);
                Decor(p,"GoldBadge","recipebook_price_round18",-130,-234,140,42);
                Decor(p,"GoldCoin","Coin_Icon",-204,-234,65,65);
                // 금액 배경이 동전을 덮지 않도록 아이콘과 숫자를 앞에 표시합니다.
                p.Find("GoldCoin").SetAsLastSibling();p.Find("GoldCost").SetAsLastSibling();
                Place(p.Find("UpgradeButton"),135,-219,248,80);Place(p.Find("UpgradeButtonShadow"),135,-228,248,80);
                Place(p.Find("UpgradeButton/Label"),0,0,248,80);Label(p.Find("UpgradeButton/Label"),44,false,Cream);
            }
            else
            {
                Place(p.Find("Title"),1,363,500,60);Label(p.Find("Title"),36,false,Brown,"안내");
                Place(p.Find("QuantityFoodBackdrop"),-151,190,230,210);Picture(p.Find("QuantityFoodBackdrop"),"popup_text_background_round18").pixelsPerUnitMultiplier=18f/35f;
                FitMenuIconWithMargin(p.Find("FoodIcon"),-151,190,230,210);
                Place(p.Find("DishName"),150,222,300,60);Label(p.Find("DishName"),34);
                var dishName=p.Find("DishName").GetComponent<TMP_Text>();dishName.alignment=TextAlignmentOptions.MidlineLeft;dishName.enableAutoSizing=true;dishName.fontSizeMin=22;dishName.fontSizeMax=34;dishName.enableWordWrapping=true;
                var previewCount=Ensure(p,"PreviewQuantity");Place(previewCount,-94,120,96,44);Label(previewCount,36,false,Cream);previewCount.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Right;previewCount.SetAsLastSibling();
                Place(p.Find("LevelPrice"),50,167,100,40);Label(p.Find("LevelPrice"),24,false,Cream);
                var recipe=GetComponent<IjakayaView>().MenuView.SelectedRecipe;if(recipe!=null)p.Find("LevelPrice").GetComponent<TMP_Text>().text=$"레벨 {recipe.Level}";
                Decor(p,"LevelBadge","recipebook_menu_level_round18",50,167,100,40);
                Decor(p,"QuantityPriceBadge","recipebook_price_round18",190,167,100,40);
                Place(p.Find("QuantityPriceCoin"),140,167,48,48);Place(p.Find("QuantityPriceCoin/SalePrice"),53,0,80,40);Label(p.Find("QuantityPriceCoin/SalePrice"),28);
                p.Find("QuantityPriceCoin").SetAsLastSibling();p.Find("LevelPrice").SetAsLastSibling();
                Place(p.Find("QuantityLabel"),0,46,600,40);Label(p.Find("QuantityLabel"),24,true,Brown,"메뉴를 몇 접시 등록하겠습니까?");
                Place(p.Find("MaxAmount"),0,9,600,40);Label(p.Find("MaxAmount"),24,true);
                Active(p.Find("QuantityNotice"),false);
                var heading=Ensure(p,"IngredientsHeading");Place(heading,-2,-41,500,40);Label(heading,28,false,Brown,"소모 재료");
                for(int i=0;i<4;i++){Decor(p,"MaterialPlaceholder"+i,"popup_text_background_round18",-210+i*140,-135,124,124);p.Find("MaterialPlaceholder"+i).GetComponent<Image>().pixelsPerUnitMultiplier=.6f;}
                var slots=p.GetComponentsInChildren<SushiIngredientSlotView>(true);
                for(int i=0;i<slots.Length;i++){Ingredient(slots[i].transform,-210+i*140,-135,124);Picture(slots[i].transform,"popup_text_background_round18").pixelsPerUnitMultiplier=.6f;}
                foreach(var slot in slots)
                {
                    var icon=slot.transform.Find("Icon");
                    if(icon.GetComponent<Image>().sprite?.name=="SoySauce")FitMenuIconWithMargin(icon,0,0,124,124);
                }
                Place(p.Find("ConfirmButton"),165,-322,240,80);Place(p.Find("ConfirmButtonShadow"),165,-331,240,80);
                Place(p.Find("ConfirmButton/Label"),0,3,240,80);
                Label(p.Find("ConfirmButton/Label"),36,false,Cream);
                Place(p.Find("QuantityArea"),-132,-324,290,76);Picture(p.Find("QuantityArea"),"Panel",new Color32(191,148,127,255));
                Place(p.Find("MinusButton"),-237,-324,60,60);Place(p.Find("PlusButton"),-27,-324,60,60);
                foreach(var n in new[]{"MinusButton","PlusButton"}){p.Find(n).GetComponent<Image>().color=Color.clear;Label(p.Find(n+"/Label"),36,false,Cream);}
                Place(p.Find("QuantityInput"),-132,-324,142,44);Place(p.Find("QuantityInput/Text"),0,0,135,44);Label(p.Find("QuantityInput/Text"),36,false,Cream);
                var quantityInput=p.Find("QuantityInput").GetComponent<TMP_InputField>();
                quantityInput.GetComponent<Image>().color=new Color32(170,133,117,255);
                var inputColors=quantityInput.colors;inputColors.normalColor=Color.white;inputColors.highlightedColor=Color.white;inputColors.selectedColor=Color.white;inputColors.pressedColor=Color.white;quantityInput.colors=inputColors;
            }
        }
    }

    /// <summary>기존 정산 데이터 연결을 유지하며 두 장의 영수증으로 재배치합니다.</summary>
    // 수정 구상도는 확대 화면이므로, 기존 영수증 720×930 안에 좌표와 글자 크기를 맞춥니다.
    const float SettlementX = 720f / 820f;
    const float SettlementY = 930f / 980f;
    static void SettlementPlace(Transform t,float x,float y,float w,float h)
        => Place(t,x*SettlementX,y*SettlementY,w*SettlementX,h*SettlementY);
    void SettlementLabel(Transform t,float size,bool regular=false,Color? color=null,string value=null)
        => Label(t,size*SettlementX,regular,color,value);
    void SettlementDecor(Transform p,string name,string art,float x,float y,float w,float h,Color? color=null)
        => Decor(p,name,art,x*SettlementX,y*SettlementY,w*SettlementX,h*SettlementY,color);
    public void ApplySettlement()
    {
        var page=transform.Find("BusinessResult");page.GetComponent<Image>().color=new Color(0,0,0,.6f);
        var r=page.Find("ResultVisual") ?? page;
        Active(r.Find("Title"),false);
        Decor(r,"ReceiptLeft","settlement_receipt_background_left",-365,0,720,930);
        Decor(r,"ReceiptRight","settlement_receipt_background_right",455,0,720,930);
        var sales=r.Find("SalesSummary");Place(sales,-365,230*SettlementY,750*SettlementX,300*SettlementY);sales.GetComponent<Image>().color=Color.clear;
        foreach(var n in new[]{"Header","Headline","HeadlineBackground","Coin","Divider"})Active(sales.Find(n),false);
        SettlementPlace(sales.Find("Title"),0,190,700,60);SettlementLabel(sales.Find("Title"),42,false,Brown,"총 매출");
        SettlementDecor(sales,"TotalsBorder","settlement_receipt_slot_line_round25",0,-5,750,294);
        SettlementDecor(sales,"SalesLine","settlement_receipt_line",0,65,660,3);SettlementDecor(sales,"CostLine","settlement_receipt_line",0,-15,660,3);
        string[] labels={"TotalSalesLabel","RunningCostLabel","NetProfitLabel"};string[] values={"TotalSales","RunningCost","NetProfit"};
        float[] ys={105,25,-92};
        for(int i=0;i<3;i++){
            SettlementPlace(sales.Find(labels[i]),-150,ys[i],300,60);SettlementLabel(sales.Find(labels[i]),i==2?42:34,false,i==2?Cream:Dark);sales.Find(labels[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            SettlementPlace(sales.Find(values[i]),245,ys[i],210,60);SettlementLabel(sales.Find(values[i]),i==2?42:34,false,i==2?Cream:i==1?new Color32(255,80,89,255):Dark);sales.Find(values[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
            SettlementDecor(sales,"Coin"+i,"Coin_Icon",140,ys[i],i==2?54:48, i==2?54:48);
        }
        SettlementPlace(sales.Find("ProfitBackground"),0,-92,710,110);Picture(sales.Find("ProfitBackground"),"settlement_receipt_net_profit_round25");
        sales.Find("Coin2").SetAsLastSibling();sales.Find("NetProfit").SetAsLastSibling();sales.Find("NetProfitLabel").SetAsLastSibling();
        var menus=r.Find("MenuSales");Place(menus,-365,-230*SettlementY,750*SettlementX,420*SettlementY);menus.GetComponent<Image>().color=Color.clear;
        foreach(var n in new[]{"Divider","ColumnHeaders","CountHeader","AmountHeader","Header"})Active(menus.Find(n),false);
        SettlementPlace(menus.Find("Title"),0,250,700,60);SettlementLabel(menus.Find("Title"),42,false,Brown,"판매 내역");
        var scroll=menus.GetComponentInChildren<ScrollRect>(true);SettlementPlace(scroll.transform,0,-5,750,420);Picture(scroll.transform,"settlement_receipt_slot_line_round25");SettlementPlace(scroll.viewport,-6,0,730,400);
        scroll.content.anchorMin=new Vector2(0,1);scroll.content.anchorMax=Vector2.one;scroll.content.pivot=new Vector2(.5f,1);scroll.content.anchoredPosition=Vector2.zero;
        // 기존 목록에 스크롤바가 없으면 화면 표시용으로 추가하고 기존 ScrollRect에 연결합니다.
        if(scroll.verticalScrollbar==null){
            var rail=Ensure(scroll.transform,"SalesScrollbar");var bar=rail.GetComponent<Scrollbar>()??rail.gameObject.AddComponent<Scrollbar>();
            var handle=Ensure(rail,"Handle");var hr=(RectTransform)handle;hr.anchorMin=Vector2.zero;hr.anchorMax=Vector2.one;hr.offsetMin=hr.offsetMax=Vector2.zero;var handleImage=Picture(handle,"scrollbar_inside_round5");
            bar.handleRect=(RectTransform)handle;bar.targetGraphic=handleImage;bar.direction=Scrollbar.Direction.BottomToTop;bar.value=1;bar.transition=Selectable.Transition.None;scroll.verticalScrollbar=bar;
        }
        Scroll(scroll,10*SettlementX);if(scroll.verticalScrollbar!=null){var bar=(RectTransform)scroll.verticalScrollbar.transform;bar.anchorMin=bar.anchorMax=new Vector2(1,.5f);bar.pivot=new Vector2(.5f,.5f);bar.anchoredPosition=new Vector2(-18*SettlementX,0);bar.sizeDelta=new Vector2(10*SettlementX,370*SettlementY);}
        var guests=r.Find("GuestSummary");Place(guests,455,285*SettlementY,630*SettlementX,250*SettlementY);guests.GetComponent<Image>().color=Color.clear;
        Active(guests.Find("Header"),false);Active(guests.Find("Divider"),false);
        SettlementPlace(guests.Find("Title"),0,180,630,60);SettlementLabel(guests.Find("Title"),42,false,Brown,"방문 내역");guests.Find("Title").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        var names=new[]{"Total","Satisfied","Left"};var texts=new[]{"총 방문자","만족한 손님","불만족한 손님"};
        for(int i=0;i<3;i++){
            float y=90-i*90;var bg=guests.Find("RowBackground"+i);SettlementPlace(bg,0,y,630,82);Picture(bg,"settlement_receipt_slot_line_round25");
            var label=Ensure(guests,"RowLabel"+i);SettlementPlace(label,-40,y,320,65);SettlementLabel(label,34,false,Dark,texts[i]);label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            SettlementPlace(guests.Find(names[i]),245,y,100,65);SettlementLabel(guests.Find(names[i]),32,false,Dark,"—");guests.Find(names[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
            if(i>0){SettlementDecor(guests,"Mood"+i,i==1?"satisfaction_happy_icon":"satisfaction_angry_icon",-255,y,54,54);guests.Find("Mood"+i).SetAsLastSibling();}
        }
        var staff=r.Find("StaffResults");Place(staff,455,-155*SettlementY,630*SettlementX,450*SettlementY);staff.GetComponent<Image>().color=Color.clear;
        Active(staff.Find("Header"),false);Active(staff.Find("Divider"),false);
        SettlementPlace(staff.Find("Title"),0,245,630,60);SettlementLabel(staff.Find("Title"),42,false,Brown,"직원 활동");staff.Find("Title").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        for(int i=0;i<2;i++){
            var slot=staff.Find("StaffResultSlot"+(i+1));SettlementPlace(slot,0,105-i*220,630,210);Picture(slot,"settlement_receipt_slot_line_round25");
            SettlementPlace(slot.Find("Portrait"),-210,0,180,180);Picture(slot.Find("Portrait"),"receipt_employee_photo_backround_round25");Active(slot.Find("Portrait/Placeholder"),false);
            var role=Ensure(slot,"RoleLabel");SettlementPlace(role,80,60,350,55);SettlementLabel(role,34,false,Dark,i==0?"서빙 직원":"요리 직원");role.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            SettlementDecor(slot,"ActivityBackground","settlement_receipt_net_profit_round25",95,-45,370,85);
            var heading=Ensure(slot,"ActivityHeading");SettlementPlace(heading,60,-45,260,60);SettlementLabel(heading,30,false,Dark,i==0?"서빙한 요리":"제작한 요리");heading.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            SettlementPlace(slot.Find("Activity"),240,-45,75,60);SettlementLabel(slot.Find("Activity"),30,false,Dark,"—");slot.Find("Activity").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
        }
        Active(r.Find("ConfirmButton"),false);Active(r.Find("ConfirmButtonShadow"),false);
        var hint=Ensure(r,"ContinueHint");Place(hint,455,-400*SettlementY,740*SettlementX,55*SettlementY);SettlementLabel(hint,26,false,Dark,"다음으로 넘어가려면 아무 키나 눌러주세요.");
        ApplySettlementGuide(r);
        LayoutSalesCards();
    }

    public void ApplySettlementGuide(Transform r)
    {
        Place(r.Find("ReceiptLeft"),-393,-7,720,914); Place(r.Find("ReceiptRight"),412,-7,720,914);
        var sales=r.Find("SalesSummary"); Place(sales,-398,210,620,270);
        Place(sales.Find("Title"),3,178,620,50); Label(sales.Find("Title"),38,false,Brown,"총 매출");
        Place(sales.Find("TotalsBorder"),0,0,620,270);
        Place(sales.Find("SalesLine"),0,56,550,4); Place(sales.Find("CostLine"),0,-11,550,4);
        string[] ls={"TotalSalesLabel","RunningCostLabel","NetProfitLabel"}, vs={"TotalSales","RunningCost","NetProfit"};
        float[] y={86,23,-75};
        for(int i=0;i<3;i++){
            Place(sales.Find(ls[i]),i==2?-129:-112,y[i],240,50); Label(sales.Find(ls[i]),i==2?36:30,false,i==2?Cream:Dark); sales.Find(ls[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Place(sales.Find(vs[i]),196,y[i],150,50);Label(sales.Find(vs[i]),i==2?36:30,false,i==2?Cream:i==1?new Color32(255,80,89,255):Dark);sales.Find(vs[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
            Place(sales.Find("Coin"+i),i==2?122:143,y[i],i==2?56:46,i==2?56:46);
        }
        Place(sales.Find("ProfitBackground"),0,-76,580,90);
        var menus=r.Find("MenuSales");Place(menus,-398,-221,620,388);Place(menus.Find("Title"),3,235,620,50);Label(menus.Find("Title"),38,false,Brown,"판매 내역");
        var scroll=menus.GetComponentInChildren<ScrollRect>(true);Place(scroll.transform,0,0,620,388);Place(scroll.viewport,-8,0,592,364);
        var rail=(RectTransform)scroll.verticalScrollbar.transform;rail.anchorMin=rail.anchorMax=rail.pivot=new Vector2(.5f,.5f);rail.anchoredPosition=new Vector2(292,0);rail.sizeDelta=new Vector2(10,310);
        var guests=r.Find("GuestSummary");Place(guests,401,247,520,234);Place(guests.Find("Title"),0,153,520,50);Label(guests.Find("Title"),38,false,Brown,"방문 내역");guests.Find("Title").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        string[] ns={"Total","Satisfied","Left"};
        for(int i=0;i<3;i++){
            float gy=82-i*82;Place(guests.Find("RowBackground"+i),0,gy,520,70);
            Place(guests.Find("RowLabel"+i),-15,gy,300,50);Label(guests.Find("RowLabel"+i),30,false,Dark);guests.Find("RowLabel"+i).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Place(guests.Find(ns[i]),214,gy,52,50);Label(guests.Find(ns[i]),30,false,Dark);guests.Find(ns[i]).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
            if(i>0){Place(guests.Find("Mood"+i),-205,gy,50,50);Picture(guests.Find("Mood"+i),i==1?"NPC_Needs_Face":"NPC_Needs_Face3");}
        }
        var visitorIcon=guests.Find("VisitorIcon");
        if(visitorIcon!=null){Place(visitorIcon,-205,82,58,48);foreach(Transform child in visitorIcon)Active(child,false);Picture(visitorIcon,"guest_icon");}
        var staff=r.Find("StaffResults");Place(staff,401,-166,520,384);Place(staff.Find("Title"),0,235,520,50);Label(staff.Find("Title"),38,false,Brown,"직원 활동");staff.Find("Title").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
        for(int i=0;i<2;i++){
            var slot=staff.Find("StaffResultSlot"+(i+1));Place(slot,0,103-i*192,520,180);
            ApplySettlementPortrait(slot);
            Place(slot.Find("RoleLabel"),89,50,310,40);Label(slot.Find("RoleLabel"),30,false,Dark);slot.Find("RoleLabel").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Place(slot.Find("ActivityBackground"),88,-30,320,70);
            Place(slot.Find("ActivityHeading"),72,-30,230,50);Label(slot.Find("ActivityHeading"),27,false,Cream);slot.Find("ActivityHeading").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
            Place(slot.Find("Activity"),183,-30,70,50);Label(slot.Find("Activity"),27,false,Cream);slot.Find("Activity").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
        }
        Place(r.Find("ContinueHint"),401,-385,600,38);Label(r.Find("ContinueHint"),23,false,Dark,"다음으로 넘어가려면 아무 키나 눌러주세요.");
    }

    public void ApplySettlementPortrait(Transform slot)
    {
        Decor(slot,"PortraitBackground","receipt_employee_photo_backround_round25",-166,0,154,154);
        var portrait=slot.Find("Portrait");
        if(portrait!=null)FitStaffPortraitBottom(portrait,-166,0,154,154);
    }

    /// <summary>판매량 순으로 정렬하여 같은 높이의 세로 행에 표시합니다. 긴 목록의 휠·드래그를 유지합니다.</summary>
    public void LayoutSalesCards()
    {
        var page=transform.Find("BusinessResult");
        var scroll=(page.Find("ResultVisual") ?? page).Find("MenuSales/SalesScroll").GetComponent<ScrollRect>();
        var grid=scroll.content.GetComponent<GridLayoutGroup>();if(grid!=null)grid.enabled=false;
        var fitter=scroll.content.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
        var rows=new System.Collections.Generic.List<SushiMenuSalesSlotView>();
        foreach(var row in scroll.content.GetComponentsInChildren<SushiMenuSalesSlotView>(true))if(row.gameObject.activeSelf)rows.Add(row);
        int Count(SushiMenuSalesSlotView row){int.TryParse(System.Text.RegularExpressions.Regex.Match(row.transform.Find("Count").GetComponent<TMP_Text>().text,@"\d+").Value,out int n);return n;}
        rows.Sort((a,b)=>Count(b).CompareTo(Count(a)));scroll.content.sizeDelta=new Vector2(0,Mathf.Max(364,rows.Count*74+12));
        for(int i=0;i<rows.Count;i++){
            var row=rows[i].transform;bool best=i==0&&Count(rows[i])>0;
            Place(row,-8,-43-i*74,550,62);((RectTransform)row).anchorMin=((RectTransform)row).anchorMax=new Vector2(.5f,1);
            Picture(row,best?"settlement_receipt_slot_line_round25":"receipt_employee_detail_background_round25");
            FitMenuIconWithMargin(row.Find("Icon"),-230,0,70,62);
            Place(row.Find("Name"),-93,0,190,50);Label(row.Find("Name"),26,false,Dark);row.Find("Name").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;row.Find("Name").GetComponent<TMP_Text>().textWrappingMode=TextWrappingModes.NoWrap;row.Find("Name").GetComponent<TMP_Text>().overflowMode=TextOverflowModes.Ellipsis;
            Place(row.Find("Count"),58,0,65,50);Label(row.Find("Count"),27,false,best?new Color32(249,121,47,255):Dark);
            Place(row.Find("Coin"),120,0,38,38);Picture(row.Find("Coin"),"Coin_Icon");
            Place(row.Find("Amount"),196,0,108,50);Label(row.Find("Amount"),best?30:28,false,Dark);
            var amountText=row.Find("Amount").GetComponent<TMP_Text>();
            amountText.alignment=TextAlignmentOptions.MidlineRight;amountText.margin=Vector4.zero;
            amountText.fontSizeMin=16;amountText.fontSizeMax=best?30:28;amountText.enableAutoSizing=true;
            amountText.textWrappingMode=TextWrappingModes.NoWrap;amountText.overflowMode=TextOverflowModes.Ellipsis;
            Decor(row,"BestSellerCrown","crown_icon",-258,28,44,40);row.Find("BestSellerCrown").SetAsLastSibling();Active(row.Find("BestSellerCrown"),best);
        }
    }
    #region 영업 안내와 Canvas Editor 연출
    [Header("영업 시작 연출")]
    [SerializeField, Min(.01f)] float _openingBlinkDuration = .5f;
    [SerializeField, Min(.01f)] float _openingFinalFadeDuration = .15f;
    [SerializeField, Min(0)] float _openingTextDuration = 2f;
    [Header("영업 종료 연출")]
    [SerializeField, Min(0f)] float _closingOpenDuration = 1f;
    [SerializeField, Min(0f)] float _closingTextDuration = 2f;
    [SerializeField, Min(0f), Tooltip("종료 간판의 상승이 끝나기 몇 초 전에 정산창을 표시할지 지정합니다.")]
    float _settlementOverlap = .3f;
    Coroutine _signRoutine;
    Sequence _signBlinkSequence;

    /// <summary>이미지와 문구를 한 대상으로 묶고 Canvas Editor의 저장된 효과를 연결합니다.</summary>
    public void PrepareBusinessSign(bool opening = true)
    {
        var layer = Ensure(transform, "BusinessSign");
        var rt = (RectTransform)layer;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        Picture(layer, "", new Color(0, 0, 0, .6f)).raycastTarget = true;
        // Open 원화의 투명 여백을 포함해 표시 영역이 약 1220×700이 되도록 맞춥니다.
        float signWidth = opening ? 1230f : 1279f, signHeight = opening ? 721f : 751f;
        var visual = Ensure(layer, "SignVisual"); Place(visual, 0, opening ? -10.5f : 0f, signWidth, signHeight);
        if (visual.GetComponent<CanvasGroup>() == null) visual.gameObject.AddComponent<CanvasGroup>();
        // 이전 런타임 안내가 있어도 기존 오브젝트와 참조는 유지합니다.
        foreach (var name in new[] { "Sign", "OpeningCover", "WelcomeCover", "WelcomeText", "StartText" })
        {
            var old = layer.Find(name);
            if (old != null && visual.Find(name) == null) old.SetParent(visual, false);
        }
        Decor(visual, "Sign", "restaurant_close_temporary", 0, 0, signWidth, signHeight);
        Decor(visual, "ClosedSignBackdrop", "restaurant_close_temporary", 0, 0, signWidth, signHeight);
        Decor(visual, "OpeningCover", "", 2, -110, 657, 132, new Color32(247, 210, 175, 255));
        visual.Find("OpeningCover").SetAsLastSibling();
        Decor(visual, "WelcomeCover", "", 0, -246, 657, 78, new Color32(247, 210, 175, 255));
        visual.Find("WelcomeCover").SetAsLastSibling();
        var welcome = Ensure(visual, "WelcomeText"); Place(welcome, 6, -248, 606, 73);
        Label(welcome, 52, false, new Color32(231, 107, 60, 255), "환영합니다"); welcome.SetAsLastSibling();
        var start = Ensure(visual, "StartText"); Place(start, 1, -112, 650, 132);
        Label(start, 100, false, Brown, "영업 시작!"); start.SetAsLastSibling();
        var closing = Ensure(visual, "ClosingText"); Place(closing, 2, -112, 650, 132);
        Label(closing, 100, false, Brown, "영업 종료"); closing.SetAsLastSibling();
        var closingSub = Ensure(visual, "ClosingSubText"); Place(closingSub, 1, -248, 606, 80);
        Label(closingSub, 48, false, new Color32(231, 107, 60, 255), "내일 다시 찾아주세요"); closingSub.SetAsLastSibling();
        var director = GetComponent<UIDirectorView>();
        if (director == null) director = gameObject.AddComponent<UIDirectorView>();
        var binding = director.Bindings.Find(b => b.id == "sushi-business-opening");
        if (binding == null)
        {
            binding = new UIDirectorView.Binding { id = "sushi-business-opening" };
            director.Bindings.Add(binding); ConfigureBusinessOpening(binding);
        }
        binding.owner = this; binding.target = (RectTransform)visual;
        binding.enterMethod = nameof(EnterBusinessSign); binding.exitMethod = nameof(ExitBusinessSign);
        var closingBinding = director.Bindings.Find(b => b.id == "sushi-business-closing");
        if (closingBinding == null)
        {
            closingBinding = new UIDirectorView.Binding { id = "sushi-business-closing" };
            // 시작의 이동 형태를 복사하되 종료용 시간은 독립적으로 편집할 수 있게 저장합니다.
            float previousStart = 0f, previousEnd = 0f, length = 0f;
            foreach (var effect in binding.exit)
            {
                if (!effect.enabled) continue;
                previousStart = UIDirectorView.Start(effect, previousStart, previousEnd);
                previousEnd = previousStart + Mathf.Max(.01f, effect.duration);
                length = Mathf.Max(length, previousEnd);
            }
            float ratio = length > 0f ? 1.5f / length : 1f;
            foreach (var effect in binding.exit)
            {
                var copy = JsonUtility.FromJson<UIDirectorView.Effect>(JsonUtility.ToJson(effect));
                copy.duration *= ratio; copy.delay *= ratio; closingBinding.exit.Add(copy);
            }
            director.Bindings.Add(closingBinding);
        }
        closingBinding.owner = this; closingBinding.target = (RectTransform)visual;
        closingBinding.exitMethod = nameof(ExitBusinessClosingSign);
        SetBusinessSignPresentation((RectTransform)visual, false);
        if (!Application.isPlaying) Active(layer, false);
    }

    /// <summary>최초 한 번 기본 효과를 만들며 이후 편집기에서 저장한 시간과 효과는 보존합니다.</summary>
    static void ConfigureBusinessOpening(UIDirectorView.Binding binding)
    {
        UIDirectorView.Effect Move(float at, float duration, float from, float to, DG.Tweening.Ease ease) =>
            new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Move, delay = at, duration = duration,
                fromOffset = new Vector2(0, from), toOffset = new Vector2(0, to), ease = ease };
        UIDirectorView.Effect Rotate(float at, float duration, float from, float to) =>
            new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Rotate, delay = at, duration = duration,
                from = from, to = to, ease = DG.Tweening.Ease.InOutSine };
        binding.enter.Clear(); binding.exit.Clear();
        binding.enter.Add(Move(0, .5f, 1200, 0, DG.Tweening.Ease.InCubic));
        binding.enter.Add(Move(.5f, .075f, 0, 10, DG.Tweening.Ease.OutQuad));
        binding.enter.Add(Move(.575f, .075f, 10, 0, DG.Tweening.Ease.InQuad));
        binding.enter.Add(Rotate(.5f, .075f, 0, -4));
        binding.enter.Add(Rotate(.575f, .1f, -4, 2.5f));
        binding.enter.Add(Rotate(.675f, .125f, 2.5f, 0));
        binding.exit.Add(Move(0, .15f, 0, -45, DG.Tweening.Ease.InOutSine));
        binding.exit.Add(Move(.15f, .55f, -45, 1200, DG.Tweening.Ease.InCubic));
    }

    /// <summary>첨부된 완성 스프라이트를 표시하고 이전 가림판과 별도 문구를 숨깁니다.</summary>
    public void SetBusinessSignPresentation(RectTransform visual, bool opening)
    {
        if (visual == null) return;
        var group = visual.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        Picture(visual.Find("Sign"), opening ? "restaurant_open_temporary" : "restaurant_close_temporary");
        Active(visual.Find("ClosedSignBackdrop"), true);
        // 첨부된 완성 이미지 안의 문구를 그대로 사용합니다. 이전 가림판과 겹치는 TMP는 숨깁니다.
        foreach (var name in new[] { "OpeningCover", "WelcomeCover", "WelcomeText", "StartText", "ClosingText", "ClosingSubText" })
            Active(visual.Find(name), false);
    }

    /// <summary>첨부된 OPEN과 CLOSED 이미지를 공통 시간표에 맞춰 교체·페이드합니다.</summary>
    public void SampleBusinessOpeningBlink(RectTransform visual, float elapsed)
    {
        if (visual == null) return;
        // 시작과 종료는 같은 세 번의 깜빡임 간격과 마지막 페이드 시간을 공유합니다.
        bool opening = BusinessBlinkPhase(elapsed, out float fadeStart, out float fadeDuration);
        float time = elapsed + .000001f;
        SetBusinessSignPresentation(visual, opening);
        if (opening && time >= fadeStart)
        {
            float alpha = Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);
            // CLOSED 이미지를 바닥에 두고 첨부된 OPEN 이미지 전체를 페이드합니다.
            Active(visual.Find("ClosedSignBackdrop"), true);
            visual.Find("Sign").GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, alpha);
        }
    }

    /// <summary>두 번의 빠른 전환 뒤 짧은 간격을 두고 마지막 페이드를 적용하는 공통 시간표입니다.</summary>
    bool BusinessBlinkPhase(float elapsed, out float fadeStart, out float fadeDuration)
    {
        float duration = Mathf.Max(.01f, _openingBlinkDuration);
        fadeDuration = Mathf.Min(Mathf.Max(.01f, _openingFinalFadeDuration), duration * .64f);
        fadeStart = duration - fadeDuration;
        float time = elapsed + .000001f;
        return elapsed >= 0 && (time < duration * .12f ||
            time >= duration * .24f && time < duration * .36f || time >= fadeStart);
    }

    /// <summary>OPEN의 시작 문구와 CLOSED의 종료·재방문 문구를 이미지에 맞춰 선택합니다.</summary>
    public void SetBusinessClosingPresentation(RectTransform visual, bool lit)
    {
        if (visual == null) return;
        SetBusinessSignPresentation(visual, lit);
    }

    /// <summary>첨부된 OPEN과 CLOSED 이미지로 소등 연출과 마지막 페이드를 표시합니다.</summary>
    public void SampleBusinessClosingBlink(RectTransform visual, float elapsed)
    {
        if (visual == null) return;
        bool dark = BusinessBlinkPhase(elapsed, out float fadeStart, out float fadeDuration);
        if (elapsed + .000001f < fadeStart)
        {
            // 앞의 두 번은 첨부된 두 이미지 전체를 교체합니다.
            SetBusinessSignPresentation(visual, !dark);
            return;
        }

        // CLOSED를 바닥에 유지하고 OPEN만 투명해져 실제 등이 꺼지는 느낌을 냅니다.
        SetBusinessClosingPresentation(visual, true);
        float darkness = Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);
        visual.Find("Sign").GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 1f - darkness);
    }

    /// <summary>첨부된 간판 전체를 공통 시간표에 맞춰 점등합니다.</summary>
    public Sequence BuildBusinessOpeningBlink(RectTransform visual) => BuildBusinessSignBlink(visual, true);

    /// <summary>종료 연출은 같은 시간표를 반대로 사용하여 마지막에 불을 끕니다.</summary>
    public Sequence BuildBusinessClosingBlink(RectTransform visual) => BuildBusinessSignBlink(visual, false);

    /// <summary>실제 재생과 미리보기가 같은 표시 함수를 사용하는 DOTween 깜빡임입니다.</summary>
    Sequence BuildBusinessSignBlink(RectTransform visual, bool opening)
    {
        void Sample(float time)
        {
            if (opening) SampleBusinessOpeningBlink(visual, time);
            else SampleBusinessClosingBlink(visual, time);
        }
        var sequence = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
        float duration = Mathf.Max(.01f, _openingBlinkDuration);
        sequence.Append(DOVirtual.Float(0, duration, duration, Sample).SetEase(Ease.Linear));
        sequence.OnStart(() => Sample(0));
        sequence.OnComplete(() =>
        {
            if (opening) SetBusinessSignPresentation(visual, true);
            else SetBusinessClosingPresentation(visual, false);
        });
        return sequence;
    }

    /// <summary>편집기의 등장 함수입니다. 낙하와 충격 흔들림을 자식 전체에 적용합니다.</summary>
    public void EnterBusinessSign()
    {
        // <UIDirector:EnterBusinessSign>
        void __UIDirectorOriginal() { Active(transform.Find("BusinessSign/SignVisual"), true); }
        global::UIDirectorView.Dispatch(this, "EnterBusinessSign", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>편집기의 퇴장 함수입니다. 위로 사라진 뒤 안내와 입력 차단을 해제합니다.</summary>
    public void ExitBusinessSign()
    {
        // <UIDirector:ExitBusinessSign>
        void __UIDirectorOriginal() { Active(transform.Find("BusinessSign"), false); }
        global::UIDirectorView.Dispatch(this, "ExitBusinessSign", false, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>Canvas Editor에 저장된 종료 전용 퇴장 효과를 재생한 뒤 안내를 숨깁니다.</summary>
    public void ExitBusinessClosingSign()
    {
        // <UIDirector:ExitBusinessClosingSign>
        void __UIDirectorOriginal() { Active(transform.Find("BusinessSign"), false); }
        global::UIDirectorView.Dispatch(this, "ExitBusinessClosingSign", false, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>저장된 등장 효과의 길이를 계산하여 흔들림이 끝난 뒤 문구를 표시합니다.</summary>
    float BusinessEntranceDuration()
    {
        var director = GetComponent<UIDirectorView>();
        var binding = director != null ? director.Bindings.Find(b => b.id == "sushi-business-opening") : null;
        float start = 0, end = 0, duration = 0;
        if (binding == null) return 0;
        foreach (var effect in binding.enter)
        {
            if (!effect.enabled) continue;
            start = UIDirectorView.Start(effect, start, end);
            end = start + Mathf.Max(.01f, effect.duration); duration = Mathf.Max(duration, end);
        }
        return duration;
    }

    /// <summary>시작 연출이 모두 끝난 뒤 기존 영업 시작 콜백을 한 번 호출합니다.</summary>
    public void ShowBusinessOpening(System.Action completed = null) => ShowSign(true, completed);
    /// <summary>OPEN 유지 후 소등·종료 안내·공통 퇴장을 재생하고 정산 콜백을 한 번 호출합니다.</summary>
    public void ShowBusinessClosing(System.Action completed = null, System.Action settlementEntering = null)
        => ShowSign(false, completed, settlementEntering);

    /// <summary>기존 안내를 취소한 뒤 새 안내를 시작하여 이전 완료 콜백의 실행을 막습니다.</summary>
    void ShowSign(bool opening, System.Action completed, System.Action settlementEntering = null)
    {
        CancelBusinessSign();
        _signRoutine = StartCoroutine(ShowSignRoutine(opening, completed, settlementEntering));
    }

    /// <summary>시작은 낙하부터, 종료는 OPEN 1초 유지부터 재생하며 깜빡임과 퇴장 효과를 공유합니다.</summary>
    System.Collections.IEnumerator ShowSignRoutine(bool opening, System.Action completed, System.Action settlementEntering)
    {
        PrepareBusinessSign(opening);
        var layer = transform.Find("BusinessSign"); var visual = layer.Find("SignVisual");
        layer.SetAsLastSibling(); Active(layer, true);
        SetBusinessSignPresentation((RectTransform)visual, false);
        if (opening)
        {
            EnterBusinessSign();
            yield return new WaitForSecondsRealtime(BusinessEntranceDuration());
            _signBlinkSequence = BuildBusinessOpeningBlink((RectTransform)visual);
            yield return _signBlinkSequence.Play().WaitForCompletion();
            _signBlinkSequence.Kill(false); _signBlinkSequence = null;
            SetBusinessSignPresentation((RectTransform)visual, true);
            yield return new WaitForSecondsRealtime(_openingTextDuration);
        }
        else
        {
            // 종료는 낙하하지 않고 OPEN 이미지와 시작 문구가 켜진 상태에서 시작합니다.
            Active(visual, true);
            SetBusinessClosingPresentation((RectTransform)visual, true);
            yield return new WaitForSecondsRealtime(_closingOpenDuration);
            _signBlinkSequence = BuildBusinessClosingBlink((RectTransform)visual);
            yield return _signBlinkSequence.Play().WaitForCompletion();
            _signBlinkSequence.Kill(false); _signBlinkSequence = null;
            SetBusinessClosingPresentation((RectTransform)visual, false);
            yield return new WaitForSecondsRealtime(_closingTextDuration);
        }
        // 움직임의 형태는 같고, 종료용 퇴장은 Canvas Editor에 별도 저장한 1.5초 설정을 사용합니다.
        if (opening) ExitBusinessSign();
        else
        {
            ExitBusinessClosingSign();
            if (settlementEntering != null)
            {
                // 종료 간판의 마지막 빠른 상승에 정산창의 짧은 등장을 겹쳐 재생합니다.
                var director = GetComponent<UIDirectorView>();
                var binding = director != null ? director.Bindings.Find(b => b.id == "sushi-business-closing") : null;
                float start = 0f, end = 0f, duration = 0f;
                if (binding != null) foreach (var effect in binding.exit)
                {
                    if (!effect.enabled) continue;
                    start = UIDirectorView.Start(effect, start, end); end = start + Mathf.Max(.01f, effect.duration);
                    duration = Mathf.Max(duration, end);
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration - _settlementOverlap));
                settlementEntering.Invoke();
                // 두 배경의 어두움이 중첩되지 않도록 정산창의 배경으로 넘깁니다.
                var result = transform.Find("BusinessResult");
                if (result != null && result.gameObject.activeInHierarchy)
                {
                    var backdrop = layer.GetComponent<UnityEngine.UI.Image>();
                    if (backdrop != null) { var color = backdrop.color; color.a = 0f; backdrop.color = color; }
                }
            }
        }
        yield return new WaitUntil(() => layer == null || !layer.gameObject.activeSelf);
        _signRoutine = null; completed?.Invoke();
    }

    /// <summary>이 View의 안내 효과만 중지·복원하고 취소된 영업 시작 콜백은 실행하지 않습니다.</summary>
    public void CancelBusinessSign()
    {
        if (_signRoutine != null) StopCoroutine(_signRoutine);
        _signRoutine = null;
        _signBlinkSequence?.Kill(false); _signBlinkSequence = null;
        var director = GetComponent<UIDirectorView>();
        var visual = transform.Find("BusinessSign/SignVisual") as RectTransform;
        if (director != null && visual != null) director.CancelEffects(this, visual);
        Active(transform.Find("BusinessSign"), false);
    }
    void OnDisable() { CancelBusinessSign(); }
    [ContextMenu("UI 테스트/영업 시작 안내 4초")]
    void TestOpening() { if (Application.isPlaying) ShowBusinessOpening(); }
    [ContextMenu("UI 테스트/영업 종료 안내 약 5초")]
    void TestClosing() { if (Application.isPlaying) ShowBusinessClosing(); }
    #endregion
}










