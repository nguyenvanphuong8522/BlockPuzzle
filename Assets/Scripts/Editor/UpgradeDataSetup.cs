using System.IO;
using Survivor;
using UnityEditor;
using UnityEngine;

// Menu "Survivor/Create Upgrade Data": creates the companion and item assets that are missing
// (Assets/Resources/Upgrades/Companions and /Items) with the game's default text, icons and numbers.
// Existing assets are never touched, so it is safe to run again.
public static class UpgradeDataSetup
{
    const string Root = "Assets/Resources/Upgrades";
    const string CompanionDir = Root + "/Companions";
    const string ItemDir = Root + "/Items";

    [MenuItem("Survivor/Create Upgrade Data")]
    public static void CreateMissing()
    {
        Directory.CreateDirectory(CompanionDir);
        Directory.CreateDirectory(ItemDir);
        var created = 0;

        created += Make<MonkeyData>("Monkey", d =>
        {
            Card(d, 0, "Khỉ Ném Chuối", "Ném chuối như boomerang: bay ra rồi quay về, gây sát thương cả đi lẫn về.",
                new Color(1f, 0.85f, 0.25f),
                "Sát thương +33%, ném nhanh hơn.", "Ném 2 quả chuối mỗi lần.",
                "Sát thương cao hơn, chuối bay xa hơn.", "Ném 3 quả chuối mỗi lần.");
        });
        created += Make<SwordOtterData>("SwordOtter", d =>
        {
            Card(d, 1, "Rái Cá Kiếm Sĩ", "Lao tới kẻ địch gần nhất và chém một vùng hình quạt 120° phía trước.",
                new Color(0.55f, 0.85f, 1f),
                "Sát thương +25%, chém nhanh hơn.", "Vùng chém rộng hơn.",
                "Sát thương cao hơn, hồi chiêu ngắn hơn.", "Chém 2 nhát liên tiếp (qua lại).");
            d.SwingColor = d.Tint;
        });
        created += Make<TurtleMageData>("TurtleMage", d =>
        {
            Card(d, 2, "Rùa Pháp Sư", "Bắn cầu phép: trúng kẻ địch thì gây sát thương rồi nổ tung cả vùng xung quanh.",
                new Color(0.85f, 0.55f, 1f),
                "Sát thương cao hơn, vùng nổ to hơn.", "Vùng nổ to hơn, bắn nhanh hơn.",
                "Bắn 2 quả cầu mỗi lần.", "Vùng nổ cực lớn, sát thương cao nhất.");
        });
        created += Make<GunnerData>("Gunner", d =>
        {
            Card(d, 3, "Xạ Thủ Nhí", "Cầm súng tiểu liên, bắn liên tục vào kẻ địch gần nhất (0,14 giây một viên).",
                new Color(0.45f, 0.65f, 1f),
                "Sát thương cao hơn, bắn xa hơn.", "Đạn xuyên qua 1 kẻ địch.",
                "Sát thương cao hơn, bắn chụm hơn.", "Đạn xuyên qua 2 kẻ địch, sát thương cao nhất.");
        });
        created += Make<SlingFrogData>("SlingFrog", d =>
        {
            Card(d, 4, "Ếch Ná Thun", "Bắn đạn cao su nảy qua 2 kẻ địch gần nhau. Mục tiêu đứng một mình bị x2 sát thương.",
                new Color(0.5f, 0.9f, 0.4f),
                "Sát thương cao hơn, bắn nhanh hơn.", "Tầm nảy xa hơn.",
                "Sát thương cao hơn, bắn nhanh hơn.", "Sát thương cao nhất, tầm nảy xa nhất.");
        });
        created += Make<PigMaceData>("PigMace", d =>
        {
            Card(d, 5, "Lợn Cầm Trùy",
                "Lao tới nện trùy vào vùng hình quạt 120°. Mỗi 18 giây tặng hero ngẫu nhiên +ATK hoặc +Máu tối đa.",
                new Color(1f, 0.62f, 0.72f),
                "Sát thương cao hơn, quà buff lớn hơn.", "Vùng nện rộng hơn, quà buff lớn hơn.",
                "Sát thương cao hơn, hồi chiêu ngắn hơn.", "Nện 2 nhát liên tiếp, quà buff lớn nhất.");
            d.Damage = new float[] { 30, 38, 48, 60, 78 };
            d.Cooldown = new[] { 2.6f, 2.4f, 2.2f, 1.9f, 1.6f };
            d.ArcRadius = new[] { 1.6f, 1.7f, 2.0f, 2.1f, 2.3f };
            d.Swings = new[] { 1, 1, 1, 1, 2 };
            d.DashSpeed = 16f;
            d.ReturnSpeed = 11f;
            d.MaxDashTime = 0.55f;
            d.DoubleSwingGap = 0.25f;
            d.Knockback = 6f;
            d.SwingColor = Color.white;
        });
        AssetDatabase.SaveAssets();
        created += Make<WukongData>("Wukong", d =>
        {
            Card(d, 6, "Tề Thiên Đại Thánh",
                "HỢP THỂ Khỉ Ném Chuối + Rái Cá Kiếm Sĩ: ném gậy như ý khổng lồ xoay tròn 360°, bay ra quét sạch rồi bay về tay.",
                new Color(1f, 0.78f, 0.25f),
                "Sát thương cao hơn, gậy dài hơn.", "Ném 2 cây gậy mỗi lần.",
                "Sát thương cao hơn, gậy bay xa hơn.", "Ném 3 cây gậy, gậy dài nhất.");
            d.FusionOf = new CompanionData[]
            {
                AssetDatabase.LoadAssetAtPath<CompanionData>($"{CompanionDir}/Monkey.asset"),
                AssetDatabase.LoadAssetAtPath<CompanionData>($"{CompanionDir}/SwordOtter.asset"),
            };
            d.ConsumeIngredients = true;
            d.MaxLevel = 1; // one level only: no upgrade cards
        });
        created += Make<MonkData>("Monk", d =>
        {
            Card(d, 9, "Sư Thầy Gõ Mõ",
                "Ngồi gõ mõ tụng kinh: mỗi giây một vòng sóng vàng nở ra từ hero, quái chạm viền vòng thì mất máu.",
                new Color(1f, 0.78f, 0.3f),
                "Sát thương cao hơn, vòng to hơn.", "Sát thương cao hơn, vòng to hơn.",
                "Sát thương cao hơn, vòng to hơn.", "Vòng sóng lớn nhất, sát thương cao nhất.");
        });
        created += Make<ChiPheoData>("ChiPheo", d =>
        {
            Card(d, 10, "Chí Phèo",
                "Say khướt, ném chai bom xăng xanh lá vào kẻ địch gần nhất: đất bốc cháy vài giây, quái đi vào là mất máu.",
                new Color(0.45f, 0.85f, 0.4f),
                "Lửa cháy mạnh hơn, vùng cháy to hơn.", "Ném 2 chai mỗi lần, cháy lâu hơn.",
                "Lửa cháy mạnh hơn, ném nhanh hơn.", "Ném 3 chai, vùng cháy lớn nhất, cháy lâu nhất.");
        });
        created += Make<BearRifleData>("BearRifle", d =>
        {
            Card(d, 7, "Gấu Súng Săn",
                "Súng trường to, sát thương lớn, đạn xuyên: bắn 8 phát (0,34 giây một phát) rồi nạp đạn 2 giây.",
                new Color(1f, 0.6f, 0.25f),
                "Sát thương cao hơn, bắn xa hơn.", "Đạn xuyên qua thêm 1 kẻ địch.",
                "Sát thương cao hơn, bắn xa hơn.", "Đạn xuyên 3 kẻ địch, sát thương cao nhất.");
        });
        AssetDatabase.SaveAssets();
        created += Make<CowboyData>("Cowboy", d =>
        {
            Card(d, 8, "Cao Bồi Súng Máy",
                "HỢP THỂ Gấu Súng Săn + Xạ Thủ Nhí: súng máy 6 nòng khổng lồ, quay nòng rồi xả đạn liên tục, sát thương lớn.",
                new Color(0.95f, 0.7f, 0.35f));
            d.FusionOf = new CompanionData[]
            {
                AssetDatabase.LoadAssetAtPath<CompanionData>($"{CompanionDir}/BearRifle.asset"),
                AssetDatabase.LoadAssetAtPath<CompanionData>($"{CompanionDir}/Gunner.asset"),
            };
            d.ConsumeIngredients = true;
            d.MaxLevel = 1; // one level only: no upgrade cards
        });

        var sprites = Resources.Load<SpriteCatalog>("SpriteCatalog");
        created += MakeItem("Shoes", 0, "Giày Gió", "Tăng {0}% tốc độ chạy.", sprites ? sprites.ItemShoes : null,
            new Color(0.45f, 0.85f, 1f), ItemStat.MoveSpeed, 0.08f);
        created += MakeItem("Magnet", 1, "Nam Châm", "Tăng {0}% tầm hút kinh nghiệm.", sprites ? sprites.ItemMagnet : null,
            new Color(1f, 0.45f, 0.45f), ItemStat.MagnetRange, 0.25f);
        created += MakeItem("Heart", 2, "Trái Tim", "Tăng {0} máu tối đa và hồi ngay {0} máu.", sprites ? sprites.ItemHeart : null,
            new Color(1f, 0.42f, 0.55f), ItemStat.MaxHp, 20f);
        created += MakeItem("Sword", 3, "Kiếm Sắc", "Tăng {0}% sát thương cho hero và mọi đệ tử.", sprites ? sprites.ItemSword : null,
            new Color(1f, 0.7f, 0.25f), ItemStat.Attack, 0.1f);
        created += MakeItem("Regen", 4, "Bình Hồi Phục", "Hồi {0} máu mỗi giây.", sprites ? sprites.ItemRegen : null,
            new Color(0.45f, 1f, 0.6f), ItemStat.HpRegen, 0.5f);
        created += MakeItem("Armor", 5, "Giáp Sắt", "Giảm {0}% sát thương nhận vào (tối đa {1}%).", sprites ? sprites.ItemArmor : null,
            new Color(0.75f, 0.8f, 0.9f), ItemStat.DamageReduction, 0.06f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Upgrade data: {created} asset(s) created in {Root}.");
    }

    static void Card(CompanionData d, int order, string name, string description, Color tint, params string[] levels)
    {
        d.SortOrder = order;
        d.DisplayName = name;
        d.Description = description;
        d.Tint = tint;
        d.LevelDescriptions = levels;
    }

    static int Make<T>(string file, System.Action<T> fill) where T : CompanionData
    {
        var path = $"{CompanionDir}/{file}.asset";
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return 0;
        var asset = ScriptableObject.CreateInstance<T>();
        fill(asset);
        AssetDatabase.CreateAsset(asset, path);
        return 1;
    }

    static int MakeItem(string file, int order, string name, string description, Sprite icon, Color tint, ItemStat stat,
        float amount)
    {
        var path = $"{ItemDir}/{file}.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null) return 0;
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.SortOrder = order;
        item.DisplayName = name;
        item.Description = description;
        item.Icon = icon;
        item.Tint = tint;
        item.Stat = stat;
        item.AmountPerPick = amount;
        item.MaxPicks = 5;
        item.Cap = 0.3f;
        AssetDatabase.CreateAsset(item, path);
        return 1;
    }
}
