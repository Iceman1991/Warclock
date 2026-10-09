namespace DcsCampaign;

/// Startlagen Persischer Golf: Iran (Rot) gegen die Emirate, Oman und die USA (Blau). Drei Lagen zur Wahl.
static class Gulf
{
    // ---------- Gebiete (Lon, Lat) ----------
    // Iranische Küste, knapp vor dem Strand (Qeshm, Hormus, Larak liegen darüber): nördlich = Iran
    static readonly (double Lon, double Lat)[] IranCoast = [(50.0, 29.6), (51.0, 28.4), (52.0, 27.6), (53.0, 26.95), (53.7, 26.6), (54.4, 26.4), (55.2, 26.5), (55.9, 26.58),
        (56.2, 26.75), (56.5, 26.95), (56.9, 26.7), (57.2, 26.2), (57.5, 25.7), (58.0, 25.5), (59.5, 25.3)];
    // Emirate + Musandam + Batina-Küste (Oman), knapp vor der Küste; Inseln liegen draußen
    static readonly (double Lon, double Lat)[] Arabia = [(51.8, 24.0), (52.6, 24.25), (53.6, 24.2), (54.25, 24.47), (54.36, 24.56), (54.5, 24.59), (54.68, 24.66), (54.7, 24.83), (54.85, 24.9), (55.0, 25.06), (55.15, 25.17), (55.3, 25.33), (55.6, 25.62), (55.95, 25.85),
        (56.1, 26.12), (56.22, 26.42), (56.5, 26.42), (56.45, 26.0), (56.42, 25.6), (56.45, 25.0), (56.9, 24.4), (57.4, 23.9), (57.4, 22.5), (51.8, 22.5)];
    // Iranische Inseln (Lat, Lon, Radius km)
    static readonly (double Lat, double Lon, double Km)[] Islands = [(25.875, 55.03, 7), (26.26, 55.30, 5), (26.24, 55.15, 4), (25.91, 54.54, 5), (26.29, 54.52, 5), (26.53, 53.98, 12),
        (26.80, 53.36, 12), (26.68, 53.62, 5), (26.86, 56.36, 6), (27.06, 56.46, 6)];
    // Von Iran besetzt: Musandam + Ras al-Khaimah bis vor Umm al-Quwain und Dibba
    static readonly (double Lon, double Lat)[] North = [(55.6, 25.7), (55.75, 25.62), (56.05, 25.5), (56.32, 25.55), (56.6, 25.55), (56.6, 26.6), (55.6, 26.6)];
    // Von Iran besetzt: alles nördlich von Dubai, Al Madam und Fujairah
    static readonly (double Lon, double Lat)[] ToDubai = [(55.2, 25.32), (55.45, 25.30), (55.7, 25.25), (56.0, 25.22), (56.25, 25.25), (56.6, 25.25), (56.6, 26.6), (55.2, 26.6)];

    static bool InIran(double lat, double lon) => lat > Scenario.LineLat(IranCoast, lon) || Islands.Any(i => Math.Pow((lat - i.Lat) * 111, 2) + Math.Pow((lon - i.Lon) * 100, 2) < i.Km * i.Km);
    public static bool Land(double lat, double lon) => InIran(lat, lon) || Scenario.In(Arabia, lon, lat);

    static Func<double, double, Side> Owner((double Lon, double Lat)[]? red) => (lat, lon) =>
        lon < 51.8 ? Side.None                                                // Katar, Saudi-Arabien
        : InIran(lat, lon) ? Side.Red
        : Scenario.In(Arabia, lon, lat) ? (red != null && Scenario.In(red, lon, lat) ? Side.Red : Side.Blue)
        : Side.None;                                                         // offene See
    static bool InWar(double x, double z) { var (lat, lon) = Geo.ToLL(x, z); return lon >= 51.8 && Scenario.In(Arabia, lon, lat); }

    // ---------- gemeinsam ----------
    // Hinterland Iran und Inseln: keine Straße zur Front, Ziele für Luftangriffe. Hafen Bandar Abbas = Herkunft der iranischen Frachter.
    static readonly (OK K, Side S, string At, string Name, double Value, string Sys)[] Iran =
    [
        (OK.Port, Side.Red, "Rajaei Port", "Port Bandar Abbas", 5, ""),
        (OK.Factory, Side.Red, "Bandar Abbas", "Refinery Bandar Abbas", 4, ""),
        (OK.Factory, Side.Red, "Qeshm Island", "Shipyard Qeshm", 3, ""),
        (OK.Factory, Side.Red, "Tola Industrial City", "Industrial Area Bandar Abbas", 3, ""),
        (OK.Port, Side.Red, "Bandar-e Lengeh", "Port Bandar Lengeh", 3, ""),
        (OK.Factory, Side.Red, "Siri Island", "Oil Terminal Sirri", 3, ""),
        (OK.Cp, Side.Red, "Qeshm Island", "HQ IRGC Navy Qeshm", 3, ""),
        (OK.Cp, Side.Red, "Bastak", "Missile Brigade Bastak", 4, ""),
        (OK.Cp, Side.Red, "Bandar Khamir", "Drone Base Bandar Khamir", 3, ""),
        (OK.Cp, Side.Red, "Bandar Abbas", "Navy HQ Bandar Abbas", 5, ""),
        (OK.Factory, Side.Red, "Lar", "Refinery Lar", 3, ""),
        (OK.Factory, Side.Red, "Minab", "Power Plant Minab", 2, ""),
        (OK.Cp, Side.Red, "Jask", "Naval Base Jask", 3, ""),
        (OK.Cp, Side.Red, "Hajiabad", "HQ Southern Command Hajiabad", 3, ""),
        (OK.Ewr, Side.Red, "Bandar Abbas", "Radar Bandar Abbas", 3, "EWR-RU"),
        (OK.Ewr, Side.Red, "Bandar-e Lengeh", "Radar Lengeh", 2, "EWR-RU2"),
        (OK.Ewr, Side.Red, "Abu Musa Island", "Radar Abu Musa", 3, "EWR-RU"),
        (OK.Sam, Side.Red, "Bandar Abbas", "SA-10 Bandar Abbas", 5, "SA-10"),
        (OK.Sam, Side.Red, "Tola Industrial City", "SA-15 Refinery", 2, "SA-15"),
        (OK.Sam, Side.Red, "Qeshm Island", "Hawk Qeshm", 3, "Hawk"),
        (OK.Sam, Side.Red, "Hormoz", "SA-2 Hormuz", 3, "SA-2"),
        (OK.Sam, Side.Red, "Bandar-e Lengeh", "SA-6 Lengeh", 3, "SA-6"),
        (OK.Sam, Side.Red, "Abu Musa Island", "SA-15 Abu Musa", 3, "SA-15"),
        (OK.Sam, Side.Red, "Greater Tunb", "SA-15 Greater Tunb", 2, "SA-15"),
        (OK.Sam, Side.Red, "Siri Island", "Hawk Sirri", 2, "Hawk"),
        (OK.Sam, Side.Red, "Bandar Abbas", "SA-11 Bandar Abbas", 4, "SA-11"),
        (OK.Sam, Side.Red, "Qeshm Island", "SA-11 Qeshm", 3, "SA-11"),
        (OK.Sam, Side.Red, "Bastak", "SA-6 Bastak", 3, "SA-6"),
        (OK.Sam, Side.Red, "Bandar Khamir", "SA-15 Bandar Khamir", 2, "SA-15"),
    ];
    // Emirate hinten: Abu Dhabi, Jebel Ali, Industrie; Sohar (Oman) für die Einspeisung
    static readonly (OK K, Side S, string At, string Name, double Value, string Sys)[] Rear =
    [
        (OK.Depot, Side.Blue, "Abu Dhabi", "Army Depot Abu Dhabi", 4, ""),
        (OK.Factory, Side.Blue, "Emal Industrial City", "Aluminium Plant EMAL", 4, ""),
        (OK.Factory, Side.Blue, "Musaffah Industrial Area", "Industrial Area Musaffah", 3, ""),
        (OK.Port, Side.Blue, "Jebel Ali Port", "Port Jebel Ali", 5, ""),
        (OK.Port, Side.Blue, "Al Mina", "Port Mina Zayed", 3, ""),
        (OK.Cp, Side.Blue, "Abu Dhabi", "General Staff Abu Dhabi", 5, ""),
        (OK.Ewr, Side.Blue, "Abu Dhabi", "Radar Abu Dhabi", 3, "EWR-BL"),
        (OK.Ewr, Side.Blue, "Sohar", "Radar Sohar (Oman)", 2, "EWR-BL"),
        (OK.Sam, Side.Blue, "Abu Dhabi", "Patriot Abu Dhabi", 5, "Patriot"),
        (OK.Sam, Side.Blue, "Jebel Ali Port", "Patriot Jebel Ali", 4, "Patriot"),
        (OK.Depot, Side.Blue, "Al Ain", "Depot Al Ain", 3, ""),
    ];
    static readonly (string Name, double Lat, double Lon)[] Bridges =
        [("Al Maktoum Bridge (Dubai Creek)", 25.255, 55.322), ("Al Garhoud Bridge", 25.237, 55.341), ("Al Maqta Bridge (Abu Dhabi)", 24.410, 54.493)];

    const string Multi = "CAP,Escort,Sweep,Strike,SEAD,CAS,BAI,Interdiction,OCA";
    static readonly (Side S, string Country, string Type, string Name, int Base, int N, string Roles)[] Air =
    [
        (Side.Blue, "USA", "FA-18C_hornet", "VFA-87 Golden Warriors", 4, 12, Multi),
        (Side.Blue, "USA", "F-14B", "VF-103 Jolly Rogers", 4, 10, "CAP,Escort,Sweep,Strike"),
        (Side.Blue, "USA", "F-15ESE", "336th FS Rocketeers", 4, 12, "Strike,Interdiction,OCA,BAI,CAS,Escort"),
        (Side.Blue, "USA", "F-16C_50", "480th FS Warhawks", 12, 12, "SEAD,Strike,CAP,BAI,CAS"),
        (Side.Blue, "USA", "F-15C", "493rd FS Grim Reapers", 6, 8, "CAP,Sweep,Escort"),
        (Side.Blue, "USA", "A-10C_2", "74th FS Flying Tigers", 6, 10, "CAS,BAI,Interdiction"),
        (Side.Blue, "United Arab Emirates", "F-16C_50", "1st Shaheen Squadron (UAE)", 4, 12, "CAP,Escort,Strike,SEAD,BAI,CAS"),
        (Side.Blue, "United Arab Emirates", "Mirage 2000-5", "71st Squadron (UAE)", 25, 10, "CAP,Sweep,Escort,Strike"),
        (Side.Blue, "USA", "E-3A", "961st AACS", 4, 2, "AWACS"),
        (Side.Blue, "USA", "KC135MPRS", "909th ARS (Basket)", 4, 3, "Tanker"),
        (Side.Blue, "USA", "KC-135", "909th ARS (Boom)", 4, 3, "Tanker"),

        (Side.Red, "Iran", "F-14A-135-GR", "81st Fighter Squadron (Tomcat)", 19, 10, "CAP,Sweep,Escort"),
        (Side.Red, "Iran", "MiG-29A", "23rd Fighter Squadron (Fulcrum)", 11, 10, "CAP,Escort,Sweep"),
        (Side.Red, "Iran", "F-4E", "91st Fighter-Bomber Squadron (Phantom)", 2, 12, "Strike,OCA,SEAD,BAI,CAS,Interdiction"),
        (Side.Red, "Iran", "F-5E-3", "Tiger Squadron Bandar Abbas", 2, 10, "CAP,Escort,CAS,BAI"),
        (Side.Red, "Iran", "Su-24M", "72nd Bomber Squadron (Fencer)", 19, 12, "Strike,Interdiction,OCA"),
        (Side.Red, "Iran", "F-14A-135-GR", "82nd Fighter Squadron (Tomcat)", 18, 10, "CAP,Sweep,Escort"),
        (Side.Red, "Iran", "MiG-29A", "11th Fighter Squadron (Fulcrum)", 27, 10, "CAP,Escort,Sweep"),
        (Side.Red, "Iran", "F-4E", "61st Fighter-Bomber Squadron (Phantom)", 18, 12, "Strike,OCA,SEAD,BAI,Interdiction"),
        (Side.Red, "Iran", "Su-25", "IRGC Ground Attack Squadron", 9, 8, "CAS,BAI"),

        (Side.Blue, "United Arab Emirates", "AH-64D_BLK_II", "Apache Squadron Al Minhad (UAE)", 12, 8, "CAS,BAI"),
        (Side.Blue, "United Arab Emirates", "UH-1H", "Rescue Squadron Al Minhad (CSAR)", 12, 6, "CSAR"),
        (Side.Red, "Iran", "AH-1W", "IRGC Army Aviation Havadarya", 9, 8, "CAS,BAI"),
        (Side.Red, "Iran", "Mi-8MT", "IRGC Rescue Squadron (CSAR)", 9, 4, "CSAR"),

        (Side.Blue, "USA", "C-130J-30", "39th Airlift Squadron", 4, 6, "Transport"),
        (Side.Blue, "United Arab Emirates", "C-17A", "Transport Squadron Al Dhafra (UAE)", 4, 2, "Transport"),
        (Side.Red, "Iran", "C-130", "Transport Squadron Shiraz", 19, 4, "Transport"),
        (Side.Red, "Iran", "IL-76MD", "Transport Squadron Kerman", 18, 3, "Transport"),
    ];
    public static readonly string[] IranNames = ["Ahmadi", "Hosseini", "Karimi", "Moradi", "Rahimi", "Rezaei", "Jafari", "Mohammadi", "Sadeghi", "Ghasemi", "Kazemi", "Bagheri", "Nouri", "Shirazi", "Tehrani", "Farahani", "Abbasi", "Kamali", "Yazdani", "Soltani"];
    static readonly string[] EmiratiNames = ["Al Mansouri", "Al Mazrouei", "Al Ketbi", "Al Shamsi", "Al Nuaimi", "Al Dhaheri", "Al Hammadi", "Al Kaabi", "Al Suwaidi", "Al Falasi"];
    static readonly (Side S, string Name, double X, double Z, double Alt, string Task)[] Support =
        [(Side.Blue, "Magic", -189697, -106702, 9000, "AWACS"), (Side.Blue, "Texaco", -156114, -131554, 7000, "Tanker"), (Side.Blue, "Shell", -195849, -50993, 7600, "Tanker")];
    static readonly (Side S, double X, double Z, string From)[] RedSea = [(Side.Red, 31193, -48867, "Port Bandar Abbas"), (Side.Red, -30320, 65740, "Port Bandar Abbas"), (Side.Red, 12000, -168000, "Port Bandar Abbas")];   // Golf, Golf von Oman (Ostküste), vor Lengeh – alle aus Bandar Abbas
    static readonly (Side S, double X, double Z, string From)[] BlueSea = [(Side.Blue, -129982, 85847, ""), (Side.Blue, -115771, -216725, "")];   // Golf von Oman, westlicher Golf (südlich an Sir Bu Nair vorbei)

    static Setup Base(string id, string name, string text, string start, (double Lon, double Lat)[]? red) => new()
    {
        Id = id, Map = "PersianGulf", Name = name, Text = text, Start = start, Cv = (25.0, 53.8),   // Träger: Golf nördlich Abu Dhabi
        BlueText = "Blue – Emirates, Oman + USA", RedText = "Red – Iran",
        RedC = "Iran", BlueC = "United Arab Emirates", BlueSite = "United Arab Emirates", RedIds = [34], BlueIds = [2, 73, 74],
        BullBlue = new(-210842, -173003), BullRed = new(111644, 4434),        // Al Dhafra bzw. Bandar Abbas
        Owner = Owner(red), InWar = InWar, Bridges = Bridges, Sqns = Air, Support = Support,
        AirReinf = [(36, Side.Blue, "USA", "F-15C", "44th FS Vampire Bats", 4, 10, "CAP,Sweep,Escort"), (48, Side.Red, "Iran", "F-4E", "31st Fighter-Bomber Squadron (Phantom)", 18, 10, "Strike,OCA,SEAD,BAI,Interdiction"),
                    (72, Side.Blue, "USA", "F-16C_50", "555th FS Triple Nickel", 4, 12, "SEAD,Strike,CAP,BAI,CAS")],
        Cargo = port => port.Name.Contains("Jebel Ali") || port.Name.Contains("Fujairah") ? (300, 600) : (400, 400),   // Ölhäfen
        Rockets = [(Side.Red, "Missile Brigade Bastak", 6), (Side.Red, "Drone Base Bandar Khamir", 8)],
        Tels = [(Side.Red, "Bastak", 3, "Scud")],
        RedNames = IranNames, BlueNames = Scenario.UsNames, BlueFirst = Scenario.UsFirst, HostNames = EmiratiNames, Temp = 29,
    };

    // ---------- 1: Landung in den Nordemiraten ----------
    public static readonly Setup Landing = Base("golf-landung", "Gulf: Landing in the Northern Emirates",
        "Iran has landed in Musandam and Ras al-Khaimah. Land front across the Northern Emirates, three axes toward Sharjah, Al Ain and Fujairah. Iranian supply only by sea from Bandar Abbas.",
        "War in the Gulf: Iranian marines hold Musandam and Ras al-Khaimah and attack south. The USA supports the Emirates from the air.", North) with
    {
        BlueBases = [4, 5, 6, 7, 12, 14, 17, 20, 22, 23, 25, 29],
        Axes =
        [
            ("Coast (Sharjah)", Side.Red, ["Khasab", "Ras Al Khaimah", "Umm Al Quwain", "Ajman", "Sharjah", "Dubai"]),
            ("Center (Al Dhaid)", Side.Red, ["Ras Al Khaimah", "Al Usayli", "Al Dhaid", "Al Madam", "Al Ain"]),
            ("East Coast (Fujairah)", Side.Red, ["Dibba Al-hisn", "Dadna", "Al Qadisa", "Fujairah", "Khalba"]),
        ],
        Sea = [.. RedSea, .. BlueSea],
        Gates = [(Side.Blue, "Abu Dhabi", 300, 450), (Side.Blue, "Sohar", 150, 250)],   // Straße aus Saudi-Arabien und Oman
        Oob =
        [
            (Side.Red, UK.Mech, 0, "front", "Ras Al Khaimah", "IR-Mech", "IRGC Marines 1st Brigade 1st Bn"),
            (Side.Red, UK.Armor, 0, "front", "Ras Al Khaimah", "IR-Armor", "Artesh 37th Armored Brigade 1st Bn"),
            (Side.Red, UK.Art, 0, "front", "Ras Al Khaimah", "IR-Art", "IRGC Artillery Bn Ras al-Khaimah"),
            (Side.Red, UK.Shorad, 0, "front", "Ras Al Khaimah", "IR-Shorad", "IRGC Air Defense North"),
            (Side.Red, UK.Mech, 1, "front", "Al Usayli", "IR-Mech", "IRGC Marines 1st Brigade 2nd Bn"),
            (Side.Red, UK.Motor, 1, "front", "Al Usayli", "IR-Motor", "IRGC Saberin Bn"),
            (Side.Red, UK.Motor, 2, "front", "Dibba Al-hisn", "IR-Motor", "IRGC Marines Bn Dibba"),
            (Side.Red, UK.Mech, 2, "front", "Dibba Al-hisn", "IR-Mech", "IRGC Marines 1st Brigade 3rd Bn"),
            (Side.Red, UK.Armor, 0, "reserve", "Ras Al Khaimah", "IR-Armor", "Artesh 37th Armored Brigade 2nd Bn"),
            (Side.Red, UK.Mech, 0, "reserve", "Khasab", "IR-Mech", "IRGC Marines Reserve Khasab"),

            (Side.Blue, UK.Mech, 0, "front", "Umm Al Quwain", "AE-Mech", "UAE 1st Mech Brigade 11th Bn"),
            (Side.Blue, UK.Armor, 0, "front", "Umm Al Quwain", "AE-Armor", "UAE 3rd Armored Brigade 31st Bn"),
            (Side.Blue, UK.Art, 0, "front", "Ajman", "AE-Art", "UAE Artillery Brigade North"),
            (Side.Blue, UK.Shorad, 0, "front", "Umm Al Quwain", "AE-Shorad", "UAE Air Defense North"),
            (Side.Blue, UK.Mech, 1, "front", "Al Dhaid", "AE-Mech", "UAE 1st Mech Brigade 12th Bn"),
            (Side.Blue, UK.Motor, 1, "front", "Al Dhaid", "AE-Motor", "Presidential Guard Recon Bn"),
            (Side.Blue, UK.Motor, 2, "front", "Dadna", "AE-Motor", "UAE Coastal Defense Bn Fujairah"),
            (Side.Blue, UK.Mech, 2, "front", "Al Qadisa", "AE-Mech", "UAE 2nd Mech Brigade 21st Bn"),
            (Side.Blue, UK.Armor, 0, "reserve", "Sharjah", "AE-Armor", "Presidential Guard Tank Bn"),
            (Side.Blue, UK.Mech, 1, "reserve", "Dubai", "US-Mech", "1/8 Inf (US) Bradley Bn"),
            (Side.Blue, UK.Armor, 0, "reserve", "Zayed Military City", "AE-Armor", "UAE 3rd Armored Brigade 32nd Bn"),
        ],
        Objs =
        [
            (OK.Port, Side.Red, "Ras Al Khaimah", "Port Ras al-Khaimah", 4, ""),
            (OK.Port, Side.Red, "Khasab", "Port Khasab", 3, ""),
            (OK.Depot, Side.Red, "Khasab", "Army Depot Khasab", 4, ""),
            (OK.Depot, Side.Red, "Ras Al Khaimah", "Forward Depot Ras al-Khaimah", 3, ""),
            (OK.Depot, Side.Red, "Dibba Al-hisn", "Forward Depot Dibba", 3, ""),
            (OK.Cp, Side.Red, "Ras Al Khaimah", "Command Post IRGC Marines", 4, ""),
            (OK.Ewr, Side.Red, "Khasab", "Radar Khasab", 3, "EWR-RU"),
            (OK.Sam, Side.Red, "Ras Al Khaimah", "SA-15 Ras al-Khaimah", 3, "SA-15"),
            (OK.Sam, Side.Red, "Khasab", "Hawk Khasab", 3, "Hawk"),
            (OK.Sam, Side.Red, "Ras Al Khaimah", "SA-11 Ras al-Khaimah", 4, "SA-11"),
            .. Iran,
            (OK.Depot, Side.Blue, "Ajman", "Forward Depot Ajman", 3, ""),
            (OK.Depot, Side.Blue, "Al Dhaid", "Forward Depot Al Dhaid", 3, ""),
            (OK.Depot, Side.Blue, "Fujairah", "Forward Depot Fujairah", 3, ""),
            (OK.Depot, Side.Blue, "Dubai", "Army Depot Dubai", 4, ""),
            (OK.Port, Side.Blue, "Fujairah", "Port Fujairah", 4, ""),
            (OK.Port, Side.Blue, "Al Qadisa", "Port Khor Fakkan", 3, ""),
            (OK.Cp, Side.Blue, "Sharjah", "HQ Northern Command", 4, ""),
            (OK.Ewr, Side.Blue, "Fujairah", "Radar Fujairah", 2, "EWR-BL"),
            (OK.Ewr, Side.Blue, "Dubai", "Radar Dubai", 3, "EWR-BL"),
            (OK.Sam, Side.Blue, "Dubai", "Patriot Dubai", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Sharjah", "Hawk Sharjah", 3, "Hawk"),
            (OK.Sam, Side.Blue, "Fujairah", "Hawk Fujairah", 3, "Hawk"),
            (OK.Sam, Side.Blue, "Al Dhaid", "NASAMS Al Dhaid", 3, "NASAMS"),
            .. Rear,
        ],
        Decoys = [(Side.Red, "Al Usayli", "SA-6 Al Usayli", "SA-6"), (Side.Blue, "Ajman", "Hawk Ajman", "Hawk")],
        Reinf =
        [
            (18, Side.Red, UK.Mech, "Ras Al Khaimah", "IR-Mech", "IRGC Marines 2nd Brigade 1st Bn"),
            (36, Side.Red, UK.Armor, "Ras Al Khaimah", "IR-Armor", "Artesh 37th Armored Brigade 3rd Bn"),
            (54, Side.Red, UK.Art, "Khasab", "IR-Art", "Artesh Artillery Group 33"),
            (72, Side.Red, UK.Mech, "Ras Al Khaimah", "IR-Mech", "IRGC Marines 2nd Brigade 2nd Bn"),
            (30, Side.Blue, UK.Mech, "Abu Dhabi", "AE-Mech", "UAE 2nd Mech Brigade 22nd Bn"),
            (48, Side.Blue, UK.Armor, "Jebel Ali Port", "US-Armor", "3/69 Armor (US) Tank Bn"),
            (72, Side.Blue, UK.Mech, "Jebel Ali Port", "US-Mech", "2/7 Inf (US) Bradley Bn"),
            (96, Side.Blue, UK.Art, "Abu Dhabi", "US-Art", "1/41 FA (US) Artillery Bn"),
            (120, Side.Blue, UK.Motor, "Fujairah", "US-Motor", "1/2 SCR (US) Stryker Bn"),
        ],
        Caps = [("North", 0, true, Side.None, 0, 0), ("East", 2, false, Side.None, 0, 0), ("Hormuz", -1, true, Side.Red, 60843, -18733)],
    };

    // ---------- 2: Durchbruch bis vor Dubai ----------
    public static readonly Setup Dubai = Base("golf-dubai", "Gulf: Iran Outside Dubai",
        "Iran has overrun the Northern Emirates up to Sharjah, Al Dhaid and Khor Fakkan. Front outside Dubai, Al Madam and Fujairah – Blue must hold and strike back.",
        "War in the Gulf: Iranian forces stand outside Dubai and Fujairah. The USA supports the Emirates from the air.", ToDubai) with
    {
        BlueBases = [4, 5, 6, 7, 12, 17, 20, 22, 23, 25, 29],
        Axes =
        [
            ("Coast (Dubai)", Side.Red, ["Ras Al Khaimah", "Ajman", "Sharjah", "Dubai", "Jebel Ali Port", "Ghantoot", "Abu Dhabi"]),
            ("Center (Al Ain)", Side.Red, ["Masafi", "Al Dhaid", "Al Madam", "Al Hayer", "Al Ain"]),
            ("East Coast (Oman)", Side.Red, ["Dibba Al-hisn", "Al Qadisa", "Fujairah", "Khalba", "Al Shinas", "Sohar"]),
        ],
        Sea = [.. RedSea, .. BlueSea],
        Gates = [(Side.Blue, "Abu Dhabi", 300, 450), (Side.Blue, "Sohar", 150, 250)],
        Oob =
        [
            (Side.Red, UK.Mech, 0, "front", "Sharjah", "IR-Mech", "IRGC Marines 1st Brigade 1st Bn"),
            (Side.Red, UK.Armor, 0, "front", "Sharjah", "IR-Armor", "Artesh 37th Armored Brigade 1st Bn"),
            (Side.Red, UK.Art, 0, "front", "Ajman", "IR-Art", "IRGC Artillery Bn Sharjah"),
            (Side.Red, UK.Shorad, 0, "front", "Sharjah", "IR-Shorad", "IRGC Air Defense Sharjah"),
            (Side.Red, UK.Mech, 1, "front", "Al Dhaid", "IR-Mech", "IRGC Marines 1st Brigade 2nd Bn"),
            (Side.Red, UK.Motor, 1, "front", "Al Dhaid", "IR-Motor", "IRGC Saberin Bn"),
            (Side.Red, UK.Motor, 2, "front", "Al Qadisa", "IR-Motor", "IRGC Marines Bn Khor Fakkan"),
            (Side.Red, UK.Mech, 2, "front", "Al Qadisa", "IR-Mech", "IRGC Marines 1st Brigade 3rd Bn"),
            (Side.Red, UK.Armor, 0, "reserve", "Ajman", "IR-Armor", "Artesh 37th Armored Brigade 2nd Bn"),
            (Side.Red, UK.Mech, 1, "reserve", "Masafi", "IR-Mech", "Artesh 55th Airborne Brigade 1st Bn"),
            (Side.Red, UK.Mech, 0, "reserve", "Ras Al Khaimah", "IR-Mech", "IRGC Marines 2nd Brigade 1st Bn"),

            (Side.Blue, UK.Mech, 0, "front", "Dubai", "AE-Mech", "UAE 1st Mech Brigade 11th Bn"),
            (Side.Blue, UK.Armor, 0, "front", "Dubai", "AE-Armor", "UAE 3rd Armored Brigade 31st Bn"),
            (Side.Blue, UK.Art, 0, "front", "Jebel Ali Port", "AE-Art", "UAE Artillery Brigade North"),
            (Side.Blue, UK.Shorad, 0, "front", "Dubai", "AE-Shorad", "UAE Air Defense North"),
            (Side.Blue, UK.Mech, 1, "front", "Al Madam", "AE-Mech", "UAE 1st Mech Brigade 12th Bn"),
            (Side.Blue, UK.Motor, 1, "front", "Al Madam", "AE-Motor", "Presidential Guard Recon Bn"),
            (Side.Blue, UK.Motor, 2, "front", "Fujairah", "AE-Motor", "UAE Coastal Defense Bn Fujairah"),
            (Side.Blue, UK.Mech, 2, "front", "Fujairah", "AE-Mech", "UAE 2nd Mech Brigade 21st Bn"),
            (Side.Blue, UK.Armor, 0, "reserve", "Zayed Military City", "AE-Armor", "Presidential Guard Tank Bn"),
            (Side.Blue, UK.Mech, 0, "reserve", "Abu Dhabi", "US-Mech", "1/8 Inf (US) Bradley Bn"),
            (Side.Blue, UK.Armor, 1, "reserve", "Al Ain", "AE-Armor", "UAE 3rd Armored Brigade 32nd Bn"),
        ],
        Objs =
        [
            (OK.Port, Side.Red, "Ras Al Khaimah", "Port Ras al-Khaimah", 4, ""),
            (OK.Port, Side.Red, "Sharjah", "Port Sharjah", 3, ""),
            (OK.Port, Side.Red, "Al Qadisa", "Port Khor Fakkan", 3, ""),
            (OK.Depot, Side.Red, "Ras Al Khaimah", "Army Depot Ras al-Khaimah", 4, ""),
            (OK.Depot, Side.Red, "Ajman", "Forward Depot Ajman", 3, ""),
            (OK.Depot, Side.Red, "Al Dhaid", "Forward Depot Al Dhaid", 3, ""),
            (OK.Depot, Side.Red, "Al Qadisa", "Forward Depot Khor Fakkan", 3, ""),
            (OK.Cp, Side.Red, "Sharjah", "Command Post IRGC Sharjah", 4, ""),
            (OK.Ewr, Side.Red, "Ras Al Khaimah", "Radar Ras al-Khaimah", 3, "EWR-RU"),
            (OK.Sam, Side.Red, "Umm Al Quwain", "SA-11 Umm Al Quwain", 4, "SA-11"),
            (OK.Sam, Side.Red, "Ajman", "SA-15 Ajman", 3, "SA-15"),
            (OK.Sam, Side.Red, "Ras Al Khaimah", "Hawk Ras al-Khaimah", 3, "Hawk"),
            (OK.Sam, Side.Red, "Al Dhaid", "SA-6 Al Dhaid", 3, "SA-6"),
            .. Iran,
            (OK.Depot, Side.Blue, "Jebel Ali Port", "Army Depot Jebel Ali", 4, ""),
            (OK.Depot, Side.Blue, "Al Madam", "Forward Depot Al Madam", 3, ""),
            (OK.Depot, Side.Blue, "Fujairah", "Forward Depot Fujairah", 3, ""),
            (OK.Port, Side.Blue, "Fujairah", "Port Fujairah", 4, ""),
            (OK.Cp, Side.Blue, "Dubai", "HQ Dubai", 4, ""),
            (OK.Ewr, Side.Blue, "Dubai", "Radar Dubai", 3, "EWR-BL"),
            (OK.Ewr, Side.Blue, "Fujairah", "Radar Fujairah", 2, "EWR-BL"),
            (OK.Sam, Side.Blue, "Dubai", "Patriot Dubai", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Al Shinas", "Hawk Shinas", 3, "Hawk"),
            (OK.Sam, Side.Blue, "Al Madam", "NASAMS Al Madam", 3, "NASAMS"),
            .. Rear,
        ],
        Decoys = [(Side.Red, "Ras Al Khaimah", "SA-11 Ras al-Khaimah", "SA-11"), (Side.Blue, "Al Hayer", "Hawk Al Hayer", "Hawk")],
        Reinf =
        [
            (18, Side.Red, UK.Mech, "Ras Al Khaimah", "IR-Mech", "IRGC Marines 2nd Brigade 2nd Bn"),
            (36, Side.Red, UK.Armor, "Sharjah", "IR-Armor", "Artesh 37th Armored Brigade 3rd Bn"),
            (54, Side.Red, UK.Art, "Ras Al Khaimah", "IR-Art", "Artesh Artillery Group 33"),
            (72, Side.Red, UK.Mech, "Sharjah", "IR-Mech", "IRGC Marines 3rd Brigade 1st Bn"),
            (24, Side.Blue, UK.Mech, "Abu Dhabi", "AE-Mech", "UAE 2nd Mech Brigade 22nd Bn"),
            (42, Side.Blue, UK.Armor, "Jebel Ali Port", "US-Armor", "3/69 Armor (US) Tank Bn"),
            (66, Side.Blue, UK.Mech, "Jebel Ali Port", "US-Mech", "2/7 Inf (US) Bradley Bn"),
            (90, Side.Blue, UK.Art, "Abu Dhabi", "US-Art", "1/41 FA (US) Artillery Bn"),
            (110, Side.Blue, UK.Motor, "Abu Dhabi", "US-Motor", "1/2 SCR (US) Stryker Bn"),
        ],
        Caps = [("North", 0, true, Side.None, 0, 0), ("East", 2, false, Side.None, 0, 0), ("Hormuz", -1, true, Side.Red, 60843, -18733)],
    };

    // ---------- 3: Tankerkrieg ohne Landfront ----------
    public static readonly Setup Tanker = Base("golf-tanker", "Gulf: Tanker War",
        "Air and sea war without a land front: ports, refineries, island positions and freighters in the Strait of Hormuz. Whoever loses their war economy loses the war.",
        "War in the Gulf: Iran attacks ports and ships of the Emirates. The USA supports the Emirates from the air.", null) with
    {
        BlueBases = [4, 5, 6, 7, 10, 12, 14, 17, 20, 22, 23, 25, 28, 29],
        Sqns = [.. Air.Where(q => q.Name is not ("480th FS Warhawks" or "74th FS Flying Tigers"))],   // ohne Landkrieg schickt die USA weniger
        Sea = [(Side.Red, 13999, 50800, ""), (Side.Red, 6367, -224051, ""), .. BlueSea],   // iranische Frachter durch die Straße von Hormus bzw. aus dem nördlichen Golf
        Objs =
        [
            .. Iran,
            (OK.Port, Side.Blue, "Fujairah", "Port Fujairah", 5, ""),
            (OK.Port, Side.Blue, "Al Qadisa", "Port Khor Fakkan", 3, ""),
            (OK.Port, Side.Blue, "Ras Al Khaimah", "Port Ras al-Khaimah", 3, ""),
            (OK.Depot, Side.Blue, "Dubai", "Army Depot Dubai", 4, ""),
            (OK.Cp, Side.Blue, "Sharjah", "HQ Northern Command", 4, ""),
            (OK.Ewr, Side.Blue, "Khasab", "Radar Khasab (Oman)", 3, "EWR-BL"),
            (OK.Ewr, Side.Blue, "Fujairah", "Radar Fujairah", 2, "EWR-BL"),
            (OK.Sam, Side.Blue, "Dubai", "Patriot Dubai", 5, "Patriot"),
            (OK.Sam, Side.Blue, "Fujairah", "Hawk Fujairah", 3, "Hawk"),
            (OK.Sam, Side.Blue, "Khasab", "NASAMS Khasab", 3, "NASAMS"),
            (OK.Sam, Side.Blue, "Ras Al Khaimah", "Hawk Ras al-Khaimah", 3, "Hawk"),
            .. Rear,
        ],
        Caps = [("Gulf", -1, true, Side.Blue, -67857, -105083), ("Hormuz", -1, false, Side.Blue, -30277, 40699),
                ("Hormuz", -1, true, Side.Red, 60843, -18733), ("Abu Musa", -1, false, Side.Red, -12178, -124340)],
    };
}
