namespace DcsCampaign;

/// Startlage Syrien: Syrien mit russischem Kontingent (Rot) gegen Israel, Jordanien und die Türkei mit der NATO (Blau).
/// Zwei Kriegsschauplätze, vier Achsen: Golan und Jarmuk im Süden, Aleppo und Idlib–Hatay im Norden, verbunden über Homs–Hama.
/// Libanon und Irak bleiben neutral. Plätze und Lage kommen aus der Installation (Auto.Def), die Startlage ist von Hand.
static class Syria
{
    // ---------- Gebiete (Lon, Lat) ----------
    static readonly (double Lon, double Lat)[] Border = [(35.92, 35.92), (36.10, 35.83), (36.20, 35.96), (36.38, 36.03), (36.48, 36.13), (36.57, 36.22), (36.66, 36.24),
        (36.55, 36.40), (36.53, 36.55), (36.56, 36.75), (36.66, 36.83), (36.95, 36.69), (37.10, 36.665), (37.60, 36.66), (38.02, 36.84), (39.00, 36.70), (40.00, 36.82),
        (41.00, 37.08), (42.36, 37.11), (41.40, 36.50), (41.20, 34.75), (40.90, 34.40), (38.79, 33.37), (36.84, 32.31), (36.40, 32.50), (36.25, 32.53), (36.07, 32.59),
        (36.00, 32.66), (35.93, 32.71), (35.87, 32.78), (35.87, 33.00), (35.86, 33.15), (35.85, 33.25), (35.80, 33.33), (35.95, 33.48), (36.08, 33.68), (36.30, 33.82),
        (36.40, 34.00), (36.62, 34.20), (36.45, 34.55), (36.30, 34.65), (35.97, 34.64), (35.80, 34.70), (35.70, 35.40), (35.75, 35.90)];
    static readonly (double Lon, double Lat)[] Lebanon = [(35.97, 34.64), (36.30, 34.65), (36.45, 34.55), (36.62, 34.20), (36.40, 34.00), (36.30, 33.82), (36.08, 33.68),
        (35.95, 33.48), (35.80, 33.33), (35.63, 33.30), (35.58, 33.29), (35.53, 33.25), (35.50, 33.09), (35.10, 33.09), (34.90, 33.10), (35.30, 34.00), (35.80, 34.70)];
    static Side Owner(double lat, double lon) => Scenario.In(Border, lon, lat) ? Side.Red : Scenario.In(Lebanon, lon, lat) || lon > 39.3 && lat < 37.0 ? Side.None : Side.Blue;
    static bool InWar(double x, double z) { var (lat, lon) = Geo.ToLL(x, z); return lat is > 32.45 and < 37.12 && lon is > 35.2 and < 37.6; }

    /// Lat/Lon → x/z der Syrien-Karte (ohne die globale Projektion umzuschalten)
    static P XZ(double lat, double lon) { var d = Auto.Def("Syria")!; var r = Geo.Raw(lat, lon, d.Lon0); return new(Math.Round(r.X + d.FN), Math.Round(r.Z + d.FE)); }

    const string Multi = "CAP,Escort,Sweep,Strike,SEAD,CAS,BAI,Interdiction,OCA";
    static readonly (Side S, string Country, string Type, string Name, int Base, int N, string Roles)[] Air =
    [
        (Side.Red, "Syria", "MiG-29A", "697th Fighter Squadron (Fulcrum)", 9, 12, "CAP,Escort,Sweep"),
        (Side.Red, "Syria", "MiG-29S", "698th Fighter Squadron (Fulcrum)", 35, 10, "CAP,Escort,Sweep"),
        (Side.Red, "Syria", "Su-24M", "819th Bomber Squadron (Fencer)", 39, 10, "Strike,Interdiction,OCA,SEAD"),
        (Side.Red, "Russia", "Su-30", "120th Mixed Aviation Regiment", 21, 12, "Sweep,CAP,Strike,SEAD"),
        (Side.Red, "Russia", "Su-34", "47th Bomber Regiment", 21, 12, "Strike,Interdiction,OCA,BAI"),
        (Side.Red, "Russia", "Su-27", "3rd Fighter Aviation Regiment", 39, 10, "CAP,Sweep,Escort"),
        (Side.Red, "Russia", "Su-25", "368th Assault Aviation Regiment", 36, 10, "CAS,BAI"),
        (Side.Red, "Syria", "Mi-24P", "Helicopter Squadron Marj Ruhayyil", 23, 8, "CAS,BAI"),
        (Side.Red, "Russia", "Mi-24P", "55th Helicopter Regiment", 31, 8, "CAS,BAI"),
        (Side.Red, "Syria", "Mi-8MT", "Rescue Squadron Mezzeh (CSAR)", 25, 4, "CSAR"),
        (Side.Red, "Russia", "A-50", "A-50 Detachment Hmeimim", 21, 2, "AWACS"),
        (Side.Red, "Russia", "IL-76MD", "235th Transport Aviation Regiment", 21, 4, "Transport"),
        (Side.Red, "Syria", "An-26B", "Transport Squadron Mezzeh", 25, 4, "Transport"),

        (Side.Blue, "Israel", "F-16C_50", "117th Squadron First Jet", 30, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "Israel", "F-15ESE", "69th Squadron Hammers", 81, 12, "Strike,Interdiction,OCA,BAI,Escort"),
        (Side.Blue, "Israel", "F-15C", "133rd Squadron Twin Tail Knights", 220, 10, "CAP,Sweep,Escort"),
        (Side.Blue, "Israel", "F-16C_50", "253rd Squadron Negev", 62, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "Israel", "AH-64D_BLK_II", "113th Squadron Hornet", 24, 8, "CAS,BAI"),
        (Side.Blue, "Israel", "UH-1H", "Unit 669 (CSAR)", 34, 6, "CSAR"),
        (Side.Blue, "Jordan", "F-16C_50", "1st Squadron RJAF", 64, 10, "CAP,Escort,Strike,BAI,CAS"),
        (Side.Blue, "Jordan", "AH-1W", "RJAF Cobra Squadron", 19, 8, "CAS,BAI"),
        (Side.Blue, "Turkey", "F-16C_50", "181st Filo Pars", 217, 12, "CAP,Escort,Strike,SEAD,BAI,CAS"),
        (Side.Blue, "Turkey", "F-16C_50", "151st Filo Tunc", 16, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "Turkey", "F-4E", "111th Filo Panter", 90, 10, "Strike,OCA,Interdiction,BAI"),
        (Side.Blue, "Turkey", "AH-1W", "Turkish Army Aviation Cobra Bn", 15, 8, "CAS,BAI"),
        (Side.Blue, "Turkey", "UH-1H", "Rescue Squadron Hatay (CSAR)", 15, 6, "CSAR"),
        (Side.Blue, "USA", "F-15ESE", "336th FS Rocketeers", 68, 12, "Strike,Interdiction,OCA,BAI,CAS,Escort"),
        (Side.Blue, "USA", "F-16C_50", "480th FS Warhawks", 16, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "USA", "A-10C_2", "75th FS Tigersharks", 68, 10, "CAS,BAI,Interdiction"),
        (Side.Blue, "USA", "FA-18C_hornet", "VFA-87 Golden Warriors", 44, 12, Multi),
        (Side.Blue, "USA", "F-14B", "VF-103 Jolly Rogers", 44, 10, "CAP,Escort,Sweep,Strike"),
        (Side.Blue, "USA", "E-3A", "961st AACS", 44, 2, "AWACS"),
        (Side.Blue, "USA", "KC135MPRS", "909th ARS (Basket)", 16, 3, "Tanker"),
        (Side.Blue, "USA", "KC-135", "909th ARS (Boom)", 44, 3, "Tanker"),
        (Side.Blue, "USA", "C-130J-30", "37th Airlift Squadron", 16, 6, "Transport"),
        (Side.Blue, "Israel", "C-130", "131st Squadron Yellow Bird", 220, 4, "Transport"),
    ];

    static readonly string[] SyrianNames = ["Haddad", "Khoury", "Mansour", "Darwish", "Hamdan", "Saleh", "Qassem", "Ibrahim", "Youssef", "Hassan", "Nasser", "Suleiman", "Ali", "Mahmoud", "Shami", "Halabi", "Hamwi", "Assaf", "Rifai", "Zein"];
    static readonly string[] IsraeliNames = ["Cohen", "Levi", "Mizrahi", "Peretz", "Biton", "Friedman", "Shapiro", "Katz", "Avraham", "Dahan", "Golan", "Ben-David", "Segal", "Ashkenazi", "Barak", "Navon"];
    public static readonly string[] TurkishNames = ["Yilmaz", "Kaya", "Demir", "Sahin", "Celik", "Yildiz", "Aydin", "Ozturk", "Arslan", "Dogan", "Kilic", "Aslan", "Cetin", "Kurt", "Koc", "Ozdemir"];
    static readonly string[] JordanianNames = ["Al-Majali", "Al-Khasawneh", "Al-Zoubi", "Al-Fayez", "Obeidat", "Al-Rawashdeh", "Haddadin", "Al-Hmoud", "Al-Tarawneh", "Bani Hani"];

    static Setup? war, golan;
    /// Nur wenn die Karte installiert und lesbar ist
    public static IEnumerable<Setup> Sets => Theater.Installed("Syria") && Auto.Def("Syria") != null ? [war ??= Make(), golan ??= war with
    {
        Id = "syria-golan", Name = "Syria: Golan Counterstrike",
        Text = "Day two: Syrian armour holds Ghajar, Qiryat Shemona and Ramtha. Israel and Jordan counterattack while Turkey keeps Aleppo under pressure.",
        Start = "Counterstrike on the Golan: Syrian forces stand in Galilee and northern Jordan. Israeli and Jordanian brigades attack to throw them back.",
        Held = [("Ghajar", Side.Red), ("Qiryat Shemona", Side.Red), ("Ramtha", Side.Red)],
        Axes = [.. war.Axes.Select(a => a.Name.StartsWith("Golan") || a.Name.StartsWith("Yarmouk") ? a with { Push = Side.Blue } : a)],
    }] : [];

    static Setup Make() => new()
    {
        Id = "syria-two-fronts", Map = "Syria", Name = "Syria: Two-Front War",
        Text = "Syria and its Russian allies strike the Golan and northern Jordan while Turkey and NATO push on Aleppo from the north. Two theatres, four axes, joined by the Homs–Hama corridor; Lebanon and Iraq stay neutral.",
        Start = "War in Syria: Syrian and Russian forces attack the Golan Heights and the Yarmouk valley. Turkey answers with an offensive on Aleppo; the USA joins from Incirlik, Jordan and a carrier off Cyprus.",
        BlueText = "Blue – Israel, Jordan, Turkey + USA", RedText = "Red – Syria + Russia",
        RedC = "Syria", BlueC = "Israel", BlueSite = "USA", RedIds = [0, 47, 34], BlueIds = [2, 15, 3, 59, 4],
        BullBlue = XZ(32.67, 35.18), BullRed = XZ(33.40, 36.50),            // Ramat David bzw. Damaskus
        Owner = Owner, InWar = InWar, Cv = (34.10, 34.40),                  // Träger: östliches Mittelmeer südlich Zypern
        BlueBases = [2, 10, 11, 12, 13, 15, 16, 19, 20, 24, 30, 34, 41, 44, 45, 46, 47, 49, 50, 51, 56, 57, 58, 61, 62, 64, 65, 66, 67, 68, 75, 81, 88, 90, 91, 217, 218, 219, 220, 221, 222, 223],
        Neutral = [6, 32, 33, 40, 53, 54, 55, 225],                         // Libanon, Irak
        Axes =
        [
            ("Golan Heights", Side.Red, ["Damascus", "Qatana", "Kanaker", "Khan Arnabeh", "Ghajar", "Qiryat Shemona", "Hatzor Haglilit"]),
            ("Yarmouk (Irbid)", Side.Red, ["Ghabagheb", "As Sanamayn", "Khirbet Ghazaleh", "El Taebah", "Ramtha", "Irbid"]),
            ("Aleppo (Kilis)", Side.Blue, ["Aleppo", "Aykadah", "Kapdegirmeni", "Polatbey", "Kurum", "Gaziantep"]),
            ("Idlib (Hatay)", Side.Red, ["Saraqib", "Idlib", "Maarrat Misrin", "Reyhanli", "Kirikhan", "Iskenderun"]),
        ],
        Gates = [(Side.Red, "Homs", 300, 400), (Side.Red, "Damascus", 150, 250), (Side.Blue, "Afula", 300, 450), (Side.Blue, "Irbid", 150, 250), (Side.Blue, "Gaziantep", 250, 350), (Side.Blue, "Iskenderun", 200, 300)],
        Sea = [(Side.Red, XZ(34.90, 35.40).X, XZ(34.90, 35.40).Z, ""), (Side.Red, XZ(35.55, 35.35).X, XZ(35.55, 35.35).Z, ""),
               (Side.Blue, XZ(36.68, 35.90).X, XZ(36.68, 35.90).Z, ""), (Side.Blue, XZ(32.95, 34.75).X, XZ(32.95, 34.75).Z, ""), (Side.Blue, XZ(36.65, 34.75).X, XZ(36.65, 34.75).Z, "")],
        Oob =
        [
            (Side.Red, UK.Mech, 0, "front", "Khan Arnabeh", "SY-Mech", "SAA 90th Brigade 1st Bn"),
            (Side.Red, UK.Armor, 0, "front", "Khan Arnabeh", "SY-Armor", "SAA 7th Division 78th Tank Bde"),
            (Side.Red, UK.Shorad, 0, "front", "Khan Arnabeh", "SY-Shorad", "SAA 7th Division Air Defense Bn"),
            (Side.Red, UK.Art, 0, "front", "Kanaker", "SY-Art", "SAA 7th Division Artillery Rgt"),
            (Side.Red, UK.Mech, 0, "reserve", "Qatana", "RU-Mech", "Russian 810th Naval Infantry Bn"),
            (Side.Red, UK.Mech, 1, "front", "El Taebah", "SY-Mech", "SAA 5th Division 12th Bde"),
            (Side.Red, UK.Armor, 1, "front", "El Taebah", "SY-Armor", "SAA 5th Division 112th Bde"),
            (Side.Red, UK.Shorad, 1, "front", "El Taebah", "SY-Shorad", "SAA 5th Division Air Defense Bn"),
            (Side.Red, UK.Art, 1, "front", "Khirbet Ghazaleh", "SY-Art", "SAA 5th Division Artillery Rgt"),
            (Side.Red, UK.Mech, 1, "reserve", "As Sanamayn", "SY-Mech", "SAA 9th Division 43rd Bde"),
            (Side.Red, UK.Mech, 2, "front", "Aykadah", "SY-Mech", "SAA Tiger Forces 1st Bn"),
            (Side.Red, UK.Armor, 2, "front", "Aykadah", "RU-Armor", "Russian 5th Corps Tank Bn"),
            (Side.Red, UK.Shorad, 2, "front", "Aykadah", "RU-Shorad", "Russian Task Force Air Defense"),
            (Side.Red, UK.Art, 2, "front", "Aleppo", "SY-Art", "SAA Aleppo Artillery Rgt"),
            (Side.Red, UK.Mech, 2, "reserve", "Aleppo", "SY-Mech", "SAA 30th Division 1st Bde"),
            (Side.Red, UK.Mech, 3, "front", "Maarrat Misrin", "SY-Mech", "SAA 25th Division 1st Bde"),
            (Side.Red, UK.Armor, 3, "front", "Maarrat Misrin", "SY-Armor", "SAA 4th Armored Division 42nd Bde"),
            (Side.Red, UK.Shorad, 3, "front", "Maarrat Misrin", "SY-Shorad", "SAA 25th Division Air Defense Bn"),
            (Side.Red, UK.Art, 3, "front", "Idlib", "SY-Art", "SAA 25th Division Artillery Rgt"),
            (Side.Red, UK.Motor, 3, "reserve", "Saraqib", "SY-Motor", "NDF Idlib Bn"),

            (Side.Blue, UK.Armor, 0, "front", "Ghajar", "IL-Armor", "7th Armored Brigade 75th Bn"),
            (Side.Blue, UK.Mech, 0, "front", "Ghajar", "IL-Mech", "Golani Brigade 13th Bn"),
            (Side.Blue, UK.Shorad, 0, "front", "Ghajar", "IL-Shorad", "Northern Command Air Defense"),
            (Side.Blue, UK.Art, 0, "front", "Qiryat Shemona", "IL-Art", "282nd Artillery Brigade"),
            (Side.Blue, UK.Armor, 0, "reserve", "Hatzor Haglilit", "IL-Armor", "188th Armored Brigade 53rd Bn"),
            (Side.Blue, UK.Armor, 1, "front", "Ramtha", "JO-Armor", "JAF 40th Armored Brigade 2nd Bn"),
            (Side.Blue, UK.Mech, 1, "front", "Ramtha", "JO-Mech", "JAF 12th Mech Brigade 1st Bn"),
            (Side.Blue, UK.Shorad, 1, "front", "Ramtha", "JO-Shorad", "JAF Air Defense North"),
            (Side.Blue, UK.Art, 1, "front", "Irbid", "JO-Art", "JAF Northern Artillery Bn"),
            (Side.Blue, UK.Mech, 1, "reserve", "Irbid", "US-Mech", "1/8 Inf (US) Bradley Bn"),
            (Side.Blue, UK.Armor, 2, "front", "Kapdegirmeni", "TR-Armor", "5th Armored Brigade 1st Bn"),
            (Side.Blue, UK.Mech, 2, "front", "Kapdegirmeni", "TR-Mech", "20th Armored Brigade Mech Bn"),
            (Side.Blue, UK.Shorad, 2, "front", "Kapdegirmeni", "TR-Shorad", "2nd Army Air Defense Bn"),
            (Side.Blue, UK.Art, 2, "front", "Polatbey", "TR-Art", "2nd Army Artillery Rgt (Firtina)"),
            (Side.Blue, UK.Armor, 2, "reserve", "Kurum", "TR-Armor", "5th Armored Brigade 2nd Bn"),
            (Side.Blue, UK.Mech, 3, "front", "Reyhanli", "TR-Mech", "39th Mech Brigade 1st Bn"),
            (Side.Blue, UK.Armor, 3, "front", "Reyhanli", "TR-Armor", "39th Mech Brigade Tank Bn"),
            (Side.Blue, UK.Shorad, 3, "front", "Reyhanli", "TR-Shorad", "6th Corps Air Defense Bn"),
            (Side.Blue, UK.Art, 3, "front", "Kirikhan", "TR-Art", "6th Corps Artillery Rgt"),
            (Side.Blue, UK.Motor, 3, "reserve", "Iskenderun", "US-Motor", "2/2 SCR (US) Stryker Bn"),
        ],
        Objs =
        [
            (OK.Depot, Side.Red, "Damascus", "Army Depot Damascus", 4, ""),
            (OK.Depot, Side.Red, "Hama", "Army Depot Hama", 4, ""),
            (OK.Depot, Side.Red, "Aleppo", "Army Depot Aleppo", 4, ""),
            (OK.Depot, Side.Red, "Kanaker", "Forward Depot Kanaker", 3, ""),
            (OK.Depot, Side.Red, "Khirbet Ghazaleh", "Forward Depot Izraa", 3, ""),
            (OK.Depot, Side.Red, "Idlib", "Forward Depot Idlib", 3, ""),
            (OK.Factory, Side.Red, "Al Safira Military Base", "Defense Factories Al-Safira", 5, ""),
            (OK.Factory, Side.Red, "Military Research Base", "SSRC Missile Plant", 4, ""),
            (OK.Factory, Side.Red, "Homs", "Refinery Homs", 4, ""),
            (OK.Factory, Side.Red, "Baniyas", "Refinery Baniyas", 4, ""),
            (OK.Port, Side.Red, "Tartus", "Port Tartus", 5, ""),
            (OK.Port, Side.Red, "Latakia", "Port Latakia", 4, ""),
            (OK.Cp, Side.Red, "Damascus", "General Staff Damascus", 5, ""),
            (OK.Cp, Side.Red, "Saa 1st Armoured Division", "HQ 1st Corps Al-Kiswah", 4, ""),
            (OK.Cp, Side.Red, "Saa 7th Division", "HQ 7th Division Qatana", 3, ""),
            (OK.Cp, Side.Red, "Aleppo", "HQ Northern Front Aleppo", 4, ""),
            (OK.Cp, Side.Red, "Latakia", "Russian Task Force HQ Hmeimim", 5, ""),
            (OK.Cp, Side.Red, "Tartus", "Russian Naval Base Tartus", 4, ""),
            (OK.Ewr, Side.Red, "Syadf 150th Regiment", "Radar 150th ADF Regiment", 3, "EWR-RU"),
            (OK.Ewr, Side.Red, "Homs", "Radar Homs", 3, "EWR-RU2"),
            (OK.Ewr, Side.Red, "Aleppo", "Radar Aleppo", 3, "EWR-RU"),
            (OK.Ewr, Side.Red, "Latakia", "Radar Hmeimim (Russia)", 3, "EWR-RU2"),
            (OK.Sam, Side.Red, "Damascus", "SA-10 Damascus", 5, "SA-10"),
            (OK.Sam, Side.Red, "Latakia", "SA-10 Hmeimim (Russia)", 5, "SA-10"),
            (OK.Sam, Side.Red, "Syadf 24th Brigade", "SA-2 24th ADF Brigade", 3, "SA-2"),
            (OK.Sam, Side.Red, "Syadf 159th Regiment", "SA-6 159th ADF Regiment", 3, "SA-6"),
            (OK.Sam, Side.Red, "Saa 89th Brigade", "SA-11 89th Brigade", 4, "SA-11"),
            (OK.Sam, Side.Red, "Aleppo", "SA-11 Aleppo", 4, "SA-11"),
            (OK.Sam, Side.Red, "Saraqib", "SA-6 Saraqib", 3, "SA-6"),
            (OK.Sam, Side.Red, "Hama", "SA-6 Hama", 3, "SA-6"),
            (OK.Sam, Side.Red, "Homs", "SA-2 Homs", 3, "SA-2"),
            (OK.Sam, Side.Red, "Tartus", "SA-15 Tartus", 2, "SA-15"),
            (OK.Sam, Side.Red, "Al Safira Military Base", "SA-15 Al-Safira", 2, "SA-15"),

            (OK.Depot, Side.Blue, "Afula", "Army Depot Afula", 4, ""),
            (OK.Depot, Side.Blue, "Hatzor Haglilit", "Forward Depot Northern Command", 3, ""),
            (OK.Depot, Side.Blue, "Irbid", "Army Depot Irbid", 4, ""),
            (OK.Depot, Side.Blue, "Gaziantep", "Army Depot Gaziantep", 4, ""),
            (OK.Depot, Side.Blue, "Iskenderun", "Army Depot Iskenderun", 4, ""),
            (OK.Depot, Side.Blue, "Polatbey", "Forward Depot Kilis", 3, ""),
            (OK.Depot, Side.Blue, "Kirikhan", "Forward Depot Kirikhan", 3, ""),
            (OK.Factory, Side.Blue, "Haifa", "Bazan Refinery Haifa", 4, ""),
            (OK.Factory, Side.Blue, "Karmiel", "Defense Plant Karmiel", 3, ""),
            (OK.Factory, Side.Blue, "Ceyhan", "Ceyhan Oil Terminal", 4, ""),
            (OK.Factory, Side.Blue, "Iskenderun", "Iskenderun Steel Works", 4, ""),
            (OK.Factory, Side.Blue, "Osmaniye", "Osmaniye Industrial Zone", 3, ""),
            (OK.Port, Side.Blue, "Haifa", "Port Haifa", 5, ""),
            (OK.Port, Side.Blue, "Iskenderun", "Port Iskenderun", 4, ""),
            (OK.Port, Side.Blue, "Mersin", "Port Mersin", 3, ""),
            (OK.Cp, Side.Blue, "Nazareth", "IDF Northern Command HQ", 5, ""),
            (OK.Cp, Side.Blue, "Irbid", "JAF Northern Command", 4, ""),
            (OK.Cp, Side.Blue, "Gaziantep", "HQ 2nd Army (Forward)", 4, ""),
            (OK.Cp, Side.Blue, "Antakya", "HQ 6th Corps Hatay", 4, ""),
            (OK.Ewr, Side.Blue, "Netafim Military Base", "Radar Mount Meron", 3, "EWR-BL"),
            (OK.Ewr, Side.Blue, "Islahiye", "Radar Islahiye", 3, "EWR-BL"),
            (OK.Ewr, Side.Blue, "Amman", "Radar Amman", 2, "EWR-BL"),
            (OK.Sam, Side.Blue, "Haifa", "Patriot Haifa", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Nazareth", "Patriot Nazareth", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Tiberias", "Hawk Tiberias", 3, "Hawk"),
            (OK.Sam, Side.Blue, "Amman", "Patriot Amman", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Adana", "Patriot Incirlik (NATO)", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Gaziantep", "NASAMS Gaziantep", 3, "NASAMS"),
            (OK.Sam, Side.Blue, "Iskenderun", "NASAMS Iskenderun", 3, "NASAMS"),
        ],
        Bridges =
        [
            ("Daughters of Jacob Bridge", 33.008, 35.627), ("Sheikh Hussein Bridge", 32.498, 35.573), ("Yarmouk Bridge Ramtha", 32.585, 36.030),
            ("Orontes Bridge Rastan", 34.928, 36.733), ("Orontes Bridge Hama", 35.135, 36.750), ("Orontes Bridge Jisr ash-Shughur", 35.812, 36.318),
            ("Afrin River Bridge", 36.510, 36.865), ("Asi Bridge Antakya", 36.204, 36.160), ("Ceyhan River Bridge", 37.030, 35.815),
        ],
        Decoys = [(Side.Red, "Jasim", "SA-6 Jasim", "SA-6"), (Side.Blue, "Kurum", "NASAMS Kurum", "NASAMS")],
        Reinf =
        [
            (24, Side.Red, UK.Armor, "Homs", "RU-Armor", "Russian 150th Motor Rifle Div Tank Bn"),
            (36, Side.Red, UK.Mech, "Hama", "RU-Mech", "Russian 336th Naval Infantry Bn"),
            (48, Side.Red, UK.Mech, "Damascus", "SY-Mech", "Republican Guard 104th Bde"),
            (72, Side.Red, UK.Art, "Damascus", "SY-Art", "Republican Guard Artillery Rgt"),
            (30, Side.Blue, UK.Armor, "Afula", "IL-Armor", "401st Armored Brigade 9th Bn"),
            (48, Side.Blue, UK.Mech, "Gaziantep", "TR-Mech", "5th Armored Brigade 3rd Bn"),
            (54, Side.Blue, UK.Armor, "Irbid", "US-Armor", "3/69 Armor (US) Tank Bn"),
            (72, Side.Blue, UK.Art, "Iskenderun", "TR-Art", "6th Corps Artillery Rgt 2nd Bn"),
            (96, Side.Blue, UK.Mech, "Afula", "IL-Mech", "Givati Brigade 1st Bn"),
            (-8, Side.Red, UK.Armor, "Damascus", "IR-Armor", "IRGC 8th Armored Division Tank Bn"),   // H < 0: nach dem Kriegseintritt Irans
            (-20, Side.Red, UK.Mech, "Homs", "IR-Mech", "IRGC Quds Force 1st Mech Bn"),
            (-32, Side.Red, UK.Art, "Damascus", "IR-Art", "IRGC Artillery Group 2nd Bn"),
        ],
        Rockets = [(Side.Red, "SSRC Missile Plant", 4)],
        Tels = [(Side.Red, "Homs", 2, "Scud"), (Side.Red, "Damascus", 1, "Scud"), (Side.Blue, "Gaziantep", 2, "MLRS")],
        Join = [(Side.Red, "Iran enters the war on the side of Syria: IRGC armour moves through Iraq, Iranian Phantoms arrive at Syrian bases.")],
        AirReinf = [(36, Side.Blue, "USA", "F-15C", "493rd FS Grim Reapers", 16, 10, "CAP,Sweep,Escort"), (48, Side.Red, "Russia", "Su-34", "277th Bomber Regiment", 21, 12, "Strike,Interdiction,OCA,BAI"),
                    (60, Side.Blue, "USA", "F-16C_50", "555th FS Triple Nickel", 68, 12, "SEAD,Strike,CAP,BAI,CAS"), (72, Side.Red, "Russia", "Su-27", "790th Fighter Aviation Regiment", 39, 10, "CAP,Sweep,Escort"),
                    (-12, Side.Red, "Iran", "F-4E", "IRIAF 31st Fighter-Bomber Squadron", 21, 10, "Strike,OCA,SEAD,BAI,Interdiction")],
        Sqns = Air,
        Support = [(Side.Blue, "Magic", XZ(32.45, 34.85).X, XZ(32.45, 34.85).Z, 9000, "AWACS"), (Side.Blue, "Darkstar", XZ(37.20, 36.30).X, XZ(37.20, 36.30).Z, 9000, "AWACS"),
                   (Side.Blue, "Texaco", XZ(32.20, 36.60).X, XZ(32.20, 36.60).Z, 7000, "Tanker"), (Side.Blue, "Shell", XZ(37.25, 36.70).X, XZ(37.25, 36.70).Z, 7600, "Tanker"),
                   (Side.Red, "A-50", XZ(34.70, 36.30).X, XZ(34.70, 36.30).Z, 9000, "AWACS")],
        Caps = [("Golan", 0, true, Side.None, 0, 0), ("Yarmouk", 1, false, Side.None, 0, 0), ("Aleppo", 2, true, Side.None, 0, 0), ("Hatay", 3, false, Side.None, 0, 0)],
        RedNames = SyrianNames, BlueNames = Scenario.UsNames, BlueFirst = Scenario.UsFirst, HostNames = IsraeliNames,
        Names = new() { ["Russia"] = Scenario.RedNames, ["Turkey"] = TurkishNames, ["Jordan"] = JordanianNames, ["Iran"] = Gulf.IranNames },
        Temp = 22,
    };
}
