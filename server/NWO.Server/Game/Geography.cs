namespace NWO.Server.Game;

/// <summary>
/// The real geography under Aurelia (Germany on the old world map), so the land looks like the place it was and
/// players recognise where their land would be in the real world. Rivers, lakes and mountain ranges shape the land,
/// and the old cities are ruins where they really stood. Coordinates are
/// longitude, latitude; everything is turned into map parcels through NationMap. Approximate on purpose: a parcel
/// is a few kilometres across. The names are only for reading this file: players never see real place names.
/// </summary>
public static class Geography
{
    public record River(string Name, double Width, double[] Path);
    public record Lake(string Name, double Lon, double Lat, double Rx, double Ry);
    /// <summary>A mountain range: a ridge line (or a single point) with a width in degrees and a peak height 0-9.</summary>
    public record Range(string Name, double Width, double Peak, bool Forest, double[] Ridge);
    /// <summary>A named area that changes the land: forest, farm, heath, lakes, oil, coast.</summary>
    public record Area(string Name, string Kind, double Lon, double Lat, double Rx, double Ry);
    /// <summary>An old city, now ruins. Size 1 (town) to 5 (metropolis).</summary>
    public record City(string Name, double Lon, double Lat, int Size);

    public static readonly River[] Rivers =
    [
        new("Rhine", 1.3, [9.00, 47.66, 8.60, 47.60, 8.20, 47.60, 7.59, 47.56, 7.62, 47.90, 7.75, 48.58, 8.20, 48.97, 8.47, 49.49, 8.27, 50.00, 7.93, 50.08, 7.60, 50.36, 7.10, 50.73, 6.96, 50.94, 6.77, 51.23, 6.76, 51.43, 6.25, 51.83, 6.00, 51.87]),
        new("Elbe", 1.3, [14.15, 50.90, 13.74, 51.05, 13.47, 51.16, 13.00, 51.56, 12.65, 51.87, 12.25, 51.85, 11.63, 52.13, 11.97, 52.54, 11.75, 53.00, 11.00, 53.25, 10.57, 53.37, 9.99, 53.55, 9.50, 53.65, 8.70, 53.87]),
        new("Danube", 1.2, [8.50, 47.95, 9.20, 48.05, 9.99, 48.40, 10.80, 48.70, 11.42, 48.76, 12.10, 49.02, 12.57, 48.88, 13.46, 48.57, 13.80, 48.55]),
        new("Main", 0.9, [11.90, 50.10, 10.90, 49.90, 10.23, 50.05, 9.93, 49.79, 9.60, 49.80, 9.15, 49.97, 8.68, 50.11, 8.27, 50.00]),
        new("Weser", 1.0, [9.65, 51.42, 9.36, 52.10, 8.92, 52.29, 9.20, 52.60, 8.80, 53.08, 8.58, 53.55]),
        new("Moselle", 0.8, [6.38, 49.47, 6.64, 49.75, 7.00, 49.95, 7.30, 50.10, 7.60, 50.36]),
        new("Neckar", 0.7, [8.60, 48.40, 9.18, 48.78, 9.20, 49.10, 8.95, 49.40, 8.47, 49.49]),
        new("Ems", 0.7, [8.40, 51.90, 7.70, 52.30, 7.30, 52.70, 7.20, 53.20]),
        new("Oder", 1.0, [14.99, 51.15, 14.75, 51.60, 14.60, 52.00, 14.55, 52.35, 14.20, 52.80, 14.28, 53.06, 14.40, 53.30]),
        new("Saale", 0.7, [11.90, 50.30, 11.60, 50.90, 11.95, 51.48, 11.85, 51.90]),
        new("Spree", 0.6, [14.40, 51.60, 14.30, 52.00, 13.40, 52.52, 13.10, 52.40, 12.50, 52.60, 12.20, 52.90, 12.00, 52.90]),
        new("Isar", 0.6, [11.25, 47.50, 11.58, 48.14, 12.00, 48.50, 12.90, 48.80]),
        new("Inn", 0.8, [12.20, 47.60, 12.50, 48.00, 12.90, 48.25, 13.46, 48.57]),
        // the rest of the region
        new("Thames", 0.8, [-1.9, 51.7, -1.2, 51.75, -0.5, 51.45, 0.1, 51.5, 0.8, 51.5]),
        new("Severn", 0.7, [-3.5, 52.5, -2.7, 52.4, -2.2, 52.0, -2.6, 51.6]),
        new("Shannon", 0.7, [-8.0, 54.0, -8.2, 53.4, -8.6, 52.6]),
        new("Seine", 0.9, [4.7, 47.9, 4.0, 48.3, 2.4, 48.8, 1.5, 49.1, 0.2, 49.45]),
        new("Loire", 0.9, [4.0, 45.0, 3.1, 46.8, 2.4, 47.6, 1.4, 47.4, 0.7, 47.4, -0.6, 47.4, -2.2, 47.3]),
        new("Rhône", 1.0, [6.1, 46.2, 4.85, 45.75, 4.8, 44.9, 4.8, 43.9, 4.6, 43.4]),
        new("Garonne", 0.8, [0.7, 43.0, 1.4, 43.6, 0.0, 44.3, -0.6, 44.85, -1.1, 45.5]),
        new("Ebro", 0.8, [-4.0, 43.0, -1.6, 42.1, -0.9, 41.65, 0.5, 41.0, 0.85, 40.7]),
        new("Tagus", 0.9, [-1.8, 40.4, -3.7, 40.0, -5.5, 39.8, -7.0, 39.5, -8.5, 39.0, -9.1, 38.7]),
        new("Douro", 0.8, [-2.9, 41.9, -4.7, 41.6, -6.0, 41.2, -7.5, 41.1, -8.6, 41.15]),
        new("Guadalquivir", 0.8, [-2.9, 37.9, -4.8, 37.9, -6.0, 37.4, -6.35, 36.8]),
        new("Po", 1.0, [7.1, 44.7, 7.7, 45.05, 9.1, 45.15, 10.3, 45.0, 11.6, 45.0, 12.4, 44.95]),
        new("Lower Danube", 1.4, [13.8, 48.55, 14.3, 48.3, 16.37, 48.2, 17.1, 48.15, 18.8, 47.8, 19.05, 47.5, 18.9, 46.0, 19.3, 45.25, 20.45, 44.8, 22.0, 44.6, 22.6, 44.6, 24.0, 43.7, 26.0, 43.9, 27.9, 44.1, 28.2, 45.3, 29.7, 45.2]),
        new("Vistula", 1.0, [18.9, 49.6, 19.9, 50.05, 21.0, 51.2, 21.0, 52.23, 19.0, 52.9, 18.6, 53.5, 18.9, 54.35]),
        new("Dnieper", 1.3, [33.0, 55.0, 30.5, 54.2, 30.3, 52.0, 30.5, 50.45, 32.0, 49.5, 35.1, 48.5, 35.1, 47.8, 33.5, 46.8, 32.5, 46.5]),
        new("Volga", 1.4, [36.0, 57.0, 39.9, 57.6, 44.0, 56.3, 48.0, 55.8, 49.1, 55.8, 48.5, 53.2, 46.0, 51.5, 45.0, 48.7, 47.5, 46.3]),
        new("Don", 1.0, [38.5, 53.5, 39.2, 51.6, 40.5, 49.5, 42.0, 48.6, 39.7, 47.2]),
        new("Daugava", 0.9, [28.0, 55.8, 26.6, 55.9, 24.1, 56.95]),
        new("Neman", 0.8, [26.0, 53.5, 24.0, 54.4, 22.0, 55.1, 21.3, 55.3]),
        new("Glomma", 0.7, [11.0, 62.5, 11.5, 61.0, 11.0, 59.3]),
        new("Nile", 1.1, [31.2, 24.0, 32.9, 24.1, 32.6, 26.0, 31.3, 27.5, 30.8, 29.5, 31.2, 30.1, 30.5, 31.4]),
        new("Euphrates", 1.0, [38.5, 38.7, 38.3, 36.5, 40.1, 35.3, 41.9, 34.4, 43.9, 32.6, 46.0, 31.0, 47.5, 30.5]),
        new("Tigris", 0.9, [40.2, 38.1, 42.2, 37.0, 43.1, 36.3, 44.4, 33.3, 46.5, 31.4, 47.5, 30.5]),
        new("Dniester", 0.8, [23.5, 49.5, 25.5, 48.8, 27.8, 48.2, 29.5, 46.8, 30.2, 46.4]),
    ];

    public static readonly Lake[] Lakes =
    [
        new("Lake Constance", 9.35, 47.62, 0.30, 0.10), new("Chiemsee", 12.45, 47.87, 0.06, 0.05), new("Lake Starnberg", 11.32, 47.92, 0.03, 0.08),
        new("Ammersee", 11.12, 48.00, 0.03, 0.07), new("Müritz", 12.68, 53.45, 0.10, 0.12), new("Lake Schwerin", 11.45, 53.65, 0.05, 0.12),
        new("Plauer See", 12.30, 53.48, 0.07, 0.04), new("Steinhuder Meer", 9.33, 52.47, 0.05, 0.03),
        new("Lake Geneva", 6.55, 46.42, 0.25, 0.07), new("Lake Garda", 10.65, 45.62, 0.05, 0.15), new("Lake Maggiore", 8.6, 45.95, 0.04, 0.2),
        new("Lake Como", 9.25, 46.0, 0.04, 0.15), new("Balaton", 17.75, 46.85, 0.3, 0.06), new("Ladoga", 31.5, 61.0, 1.0, 0.9),
        new("Onega", 35.5, 61.7, 0.6, 0.8), new("Vänern", 13.3, 58.9, 0.6, 0.35), new("Vättern", 14.5, 58.3, 0.12, 0.45),
        new("Peipus", 27.5, 58.6, 0.4, 0.4), new("Lough Neagh", -6.45, 54.6, 0.15, 0.12), new("Saimaa", 28.5, 61.5, 0.8, 0.4),
        new("Ohrid", 20.75, 41.0, 0.08, 0.1), new("Van", 42.9, 38.6, 0.4, 0.3), new("Tuz", 33.4, 38.8, 0.25, 0.3),
    ];

    public static readonly Range[] Ranges =
    [
        new("Bavarian Alps", 0.22, 9, false, [9.80, 47.45, 10.50, 47.40, 11.00, 47.42, 11.60, 47.50, 12.20, 47.60, 12.80, 47.60, 13.05, 47.55]),
        new("Black Forest", 0.30, 6, true, [8.10, 47.75, 8.20, 48.20, 8.35, 48.75]),
        new("Swabian Jura", 0.12, 5, false, [8.80, 48.15, 9.50, 48.40, 10.40, 48.75]),
        new("Bavarian Forest", 0.30, 6, true, [12.60, 49.25, 13.30, 48.95, 13.70, 48.75]),
        new("Harz", 0.13, 5, true, [10.30, 51.82, 10.95, 51.68]),
        new("Thuringian Forest", 0.12, 5, true, [10.20, 50.85, 11.40, 50.45]),
        new("Ore Mountains", 0.15, 6, true, [12.20, 50.35, 13.90, 50.75]),
        new("Rhön", 0.15, 4, false, [9.95, 50.45]),
        new("Vogelsberg", 0.13, 4, true, [9.25, 50.50]),
        new("Sauerland", 0.22, 4, true, [7.80, 51.20, 8.60, 51.20]),
        new("Eifel", 0.30, 4, true, [6.70, 50.30]),
        new("Hunsrück", 0.15, 4, true, [6.90, 49.75, 7.60, 50.00]),
        new("Taunus", 0.10, 4, true, [8.00, 50.20, 8.60, 50.30]),
        new("Palatinate Forest", 0.22, 4, true, [7.85, 49.30]),
        new("Odenwald", 0.18, 4, true, [8.90, 49.65]),
        new("Spessart", 0.18, 3, true, [9.40, 50.00]),
        new("Fichtelgebirge", 0.15, 5, true, [11.90, 50.05]),
        new("Teutoburg Forest", 0.06, 3, true, [7.90, 52.25, 8.50, 52.00, 9.00, 51.75]),
        new("Saxon Switzerland", 0.12, 4, true, [14.10, 50.90]),
        new("Alps", 0.35, 9, false, [5.8, 44.2, 6.9, 45.9, 7.8, 46.0, 9.5, 46.4, 11.0, 47.0, 12.5, 47.1, 14.5, 47.3, 15.8, 47.6]),
        new("Pyrenees", 0.25, 8, false, [-1.8, 43.0, 0.5, 42.7, 2.5, 42.5]),
        new("Apennines", 0.25, 6, true, [8.5, 44.4, 10.5, 44.2, 12.5, 43.2, 13.6, 42.4, 15.0, 41.0, 16.2, 39.5]),
        new("Carpathians", 0.3, 7, true, [17.5, 48.6, 19.5, 49.3, 22.5, 49.0, 24.5, 47.8, 25.5, 46.2, 24.0, 45.4, 22.5, 45.3]),
        new("Dinaric Alps", 0.35, 6, true, [14.5, 45.6, 16.5, 44.3, 18.5, 43.0, 19.8, 42.4]),
        new("Balkan Mountains", 0.2, 6, true, [22.5, 43.3, 24.5, 42.8, 26.5, 42.7]),
        new("Rhodopes", 0.3, 6, true, [24.5, 41.6]),
        new("Pindus", 0.3, 6, false, [20.8, 40.5, 21.6, 39.3]),
        new("Scandinavian Mountains", 0.6, 7, false, [6.5, 58.5, 8.0, 61.0, 11.0, 63.0, 14.0, 65.5, 17.0, 68.0, 20.0, 69.5]),
        new("Scottish Highlands", 0.6, 5, false, [-6.0, 56.8, -4.0, 57.2]),
        new("Pennines", 0.25, 4, false, [-2.2, 53.6, -2.2, 54.6]),
        new("Welsh Mountains", 0.4, 4, false, [-3.8, 52.6]),
        new("Massif Central", 0.6, 5, true, [3.0, 45.3]),
        new("Vosges", 0.2, 5, true, [7.0, 48.1]),
        new("Jura", 0.15, 5, true, [6.3, 46.6, 7.5, 47.3]),
        new("Cantabrian Mountains", 0.25, 6, false, [-6.5, 43.0, -3.5, 43.1]),
        new("Sierra Nevada", 0.3, 7, false, [-3.3, 37.1]),
        new("Sistema Central", 0.2, 6, false, [-6.0, 40.3, -3.5, 40.8]),
        new("Iberian System", 0.3, 5, false, [-2.5, 41.5, -1.0, 40.5]),
        new("Caucasus", 0.4, 9, false, [40.0, 43.5, 44.0, 42.7, 47.0, 41.5]),
        new("Urals", 0.5, 5, true, [59.0, 52.0, 59.5, 58.0, 60.0, 62.0]),
        new("Atlas", 0.6, 8, false, [-8.0, 31.0, -4.0, 33.0, 0.0, 34.5, 4.0, 35.5, 8.0, 35.5]),
        new("Taurus", 0.4, 7, false, [30.0, 37.0, 34.0, 37.2, 37.0, 38.0]),
        new("Pontic Mountains", 0.3, 6, true, [36.0, 40.8, 40.0, 40.8]),
        new("Zagros", 0.5, 8, false, [45.5, 35.5, 48.0, 33.0, 51.0, 30.0]),
        new("Elburz", 0.3, 8, false, [49.0, 36.7, 52.0, 36.0, 54.5, 36.6]),
        new("Icelandic Highlands", 0.8, 6, false, [-19.0, 64.8]),
    ];

    public static readonly Area[] Areas =
    [
        new("Lüneburg Heath", "heath", 10.00, 53.10, 0.45, 0.25), new("Spreewald", "forest", 13.95, 51.85, 0.20, 0.12),
        new("Mecklenburg Lakes", "lakes", 12.50, 53.40, 0.90, 0.25), new("Magdeburg Börde", "farm", 11.40, 52.05, 0.40, 0.25),
        new("Hildesheim Börde", "farm", 9.95, 52.15, 0.30, 0.15), new("Lower Bavaria", "farm", 12.60, 48.80, 0.40, 0.20),
        new("Kraichgau", "farm", 8.80, 49.20, 0.20, 0.15), new("Uckermark", "farm", 13.80, 53.20, 0.30, 0.25),
        new("Lower Rhine", "farm", 6.40, 51.60, 0.30, 0.25), new("Emsland", "oil", 7.25, 52.60, 0.30, 0.30),
        new("Dithmarschen", "oil", 9.00, 54.05, 0.25, 0.18), new("Weser-Ems", "oil", 8.30, 52.90, 0.30, 0.20),
        new("Upper Rhine Plain", "oil", 8.15, 49.15, 0.15, 0.30), new("Ruhr", "city", 7.20, 51.48, 0.35, 0.12),
        new("Allgäu", "farm", 10.30, 47.70, 0.35, 0.15), new("Franconian Switzerland", "forest", 11.30, 49.75, 0.20, 0.15),
        new("Holstein", "farm", 10.20, 54.10, 0.45, 0.30), new("Western Pomerania", "farm", 13.50, 53.90, 0.50, 0.25),
        new("Altmark", "farm", 11.50, 52.70, 0.35, 0.25), new("Vogtland", "forest", 12.20, 50.45, 0.20, 0.15),
        new("East Frisia", "farm", 7.50, 53.40, 0.40, 0.15), new("Westphalia", "farm", 7.80, 51.85, 0.50, 0.25),
        new("Swabia", "farm", 10.20, 48.30, 0.40, 0.25), new("Franconia", "farm", 10.60, 49.60, 0.50, 0.30),
        new("Lusatia", "forest", 14.40, 51.40, 0.35, 0.25), new("Brandenburg", "farm", 13.00, 52.30, 0.80, 0.40),
        new("Saxony-Anhalt", "farm", 11.80, 51.70, 0.40, 0.30), new("Hesse", "forest", 9.00, 50.80, 0.50, 0.35),
        new("Saarland", "farm", 6.95, 49.40, 0.20, 0.15), new("Rhineland", "farm", 7.20, 50.50, 0.40, 0.30),
    ];

    public static readonly City[] Cities =
    [
        new("Berlin", 13.40, 52.52, 5), new("Hamburg", 10.00, 53.55, 5), new("Munich", 11.58, 48.14, 5), new("Cologne", 6.96, 50.94, 4),
        new("Frankfurt", 8.68, 50.11, 4), new("Stuttgart", 9.18, 48.78, 4), new("Düsseldorf", 6.78, 51.23, 4), new("Dortmund", 7.47, 51.51, 4),
        new("Essen", 7.01, 51.46, 4), new("Leipzig", 12.37, 51.34, 4), new("Bremen", 8.80, 53.08, 4), new("Dresden", 13.74, 51.05, 4),
        new("Hanover", 9.73, 52.37, 4), new("Nuremberg", 11.08, 49.45, 4), new("Duisburg", 6.76, 51.43, 3), new("Bochum", 7.22, 51.48, 3),
        new("Wuppertal", 7.15, 51.26, 3), new("Bielefeld", 8.53, 52.02, 3), new("Bonn", 7.10, 50.73, 3), new("Münster", 7.63, 51.96, 3),
        new("Karlsruhe", 8.40, 49.01, 3), new("Mannheim", 8.47, 49.49, 3), new("Augsburg", 10.90, 48.37, 3), new("Wiesbaden", 8.24, 50.08, 3),
        new("Braunschweig", 10.52, 52.27, 3), new("Kiel", 10.13, 54.32, 3), new("Chemnitz", 12.92, 50.83, 3), new("Aachen", 6.08, 50.78, 3),
        new("Halle", 11.97, 51.48, 3), new("Magdeburg", 11.63, 52.13, 3), new("Freiburg", 7.85, 47.99, 3), new("Lübeck", 10.69, 53.87, 3),
        new("Mainz", 8.27, 50.00, 3), new("Erfurt", 11.03, 50.98, 3), new("Rostock", 12.10, 54.09, 3), new("Kassel", 9.48, 51.31, 3),
        new("Potsdam", 13.06, 52.39, 2), new("Saarbrücken", 6.99, 49.24, 3), new("Osnabrück", 8.05, 52.28, 2), new("Oldenburg", 8.21, 53.14, 2),
        new("Regensburg", 12.10, 49.02, 2), new("Würzburg", 9.93, 49.79, 2), new("Ulm", 9.99, 48.40, 2), new("Göttingen", 9.93, 51.54, 2),
        new("Heidelberg", 8.69, 49.40, 2), new("Trier", 6.64, 49.75, 2), new("Koblenz", 7.60, 50.36, 2), new("Ingolstadt", 11.42, 48.76, 2),
        new("Schwerin", 11.41, 53.63, 2), new("Passau", 13.46, 48.57, 1), new("Bamberg", 10.89, 49.89, 1), new("Flensburg", 9.44, 54.79, 1),
        new("Cottbus", 14.33, 51.76, 2), new("Jena", 11.59, 50.93, 1), new("Weimar", 11.33, 50.98, 1), new("Konstanz", 9.17, 47.66, 1),
        new("Garmisch", 11.10, 47.49, 1), new("Berchtesgaden", 13.00, 47.63, 1), new("Stralsund", 13.09, 54.31, 1), new("Greifswald", 13.38, 54.09, 1),
        new("Frankfurt on the Oder", 14.55, 52.35, 1), new("Görlitz", 14.99, 51.15, 1), new("Hof", 11.92, 50.31, 1), new("Bayreuth", 11.58, 49.94, 1),
        new("Fulda", 9.68, 50.55, 1), new("Siegen", 8.02, 50.88, 1), new("Paderborn", 8.75, 51.72, 2), new("Wolfsburg", 10.79, 52.42, 2),
        new("Emden", 7.21, 53.37, 1), new("Wilhelmshaven", 8.12, 53.53, 1), new("Cuxhaven", 8.70, 53.87, 1), new("Lüneburg", 10.41, 53.25, 1),
        new("Stendal", 11.86, 52.60, 1), new("Neubrandenburg", 13.26, 53.56, 1), new("Zwickau", 12.50, 50.72, 1), new("Plauen", 12.14, 50.50, 1),
        new("Landshut", 12.15, 48.54, 1), new("Rosenheim", 12.13, 47.86, 1), new("Kempten", 10.32, 47.73, 1), new("Pforzheim", 8.70, 48.89, 1),
        new("Reutlingen", 9.21, 48.49, 1), new("Kaiserslautern", 7.77, 49.44, 1), new("Darmstadt", 8.65, 49.87, 2), new("Gießen", 8.68, 50.58, 1),
        new("Marburg", 8.77, 50.81, 1), new("Dessau", 12.25, 51.83, 1), new("Wittenberg", 12.65, 51.87, 1), new("Neuruppin", 12.80, 52.92, 1),
        new("Eberswalde", 13.82, 52.83, 1), new("Husum", 9.05, 54.48, 1), new("Celle", 10.08, 52.62, 1), new("Hamelin", 9.36, 52.10, 1),
        // the rest of the region
        new("London", -0.12, 51.5, 5), new("Paris", 2.35, 48.86, 5), new("Madrid", -3.7, 40.42, 5), new("Rome", 12.5, 41.9, 5), new("Moscow", 37.6, 55.75, 5),
        new("Istanbul", 28.97, 41.01, 5), new("Kyiv", 30.52, 50.45, 4), new("Warsaw", 21.0, 52.23, 4), new("Vienna", 16.37, 48.21, 4), new("Budapest", 19.04, 47.5, 4),
        new("Prague", 14.42, 50.08, 4), new("Amsterdam", 4.9, 52.37, 4), new("Brussels", 4.35, 50.85, 4), new("Lisbon", -9.14, 38.72, 4), new("Barcelona", 2.17, 41.39, 4),
        new("Milan", 9.19, 45.46, 4), new("Naples", 14.27, 40.85, 4), new("Athens", 23.73, 37.98, 4), new("Stockholm", 18.07, 59.33, 4), new("Copenhagen", 12.57, 55.68, 4),
        new("Oslo", 10.75, 59.91, 3), new("Helsinki", 24.94, 60.17, 3), new("Dublin", -6.26, 53.35, 3), new("Manchester", -2.24, 53.48, 4), new("Birmingham", -1.9, 52.48, 4),
        new("Glasgow", -4.25, 55.86, 3), new("Edinburgh", -3.19, 55.95, 3), new("Lyon", 4.84, 45.76, 3), new("Marseille", 5.37, 43.3, 4), new("Toulouse", 1.44, 43.6, 3),
        new("Bordeaux", -0.58, 44.84, 3), new("Nantes", -1.55, 47.22, 3), new("Lille", 3.06, 50.63, 3), new("Valencia", -0.38, 39.47, 3), new("Seville", -5.98, 37.39, 3),
        new("Porto", -8.61, 41.15, 3), new("Turin", 7.69, 45.07, 3), new("Florence", 11.25, 43.77, 3), new("Venice", 12.33, 45.44, 3), new("Zurich", 8.54, 47.38, 3),
        new("Geneva", 6.14, 46.2, 2), new("Bern", 7.45, 46.95, 2), new("Krakow", 19.94, 50.06, 3), new("Gdansk", 18.65, 54.35, 3), new("Wroclaw", 17.03, 51.11, 3),
        new("Poznan", 16.93, 52.41, 2), new("Lodz", 19.46, 51.76, 3), new("Minsk", 27.56, 53.9, 4), new("Vilnius", 25.28, 54.69, 3), new("Riga", 24.1, 56.95, 3),
        new("Tallinn", 24.75, 59.44, 2), new("Saint Petersburg", 30.31, 59.94, 5), new("Bucharest", 26.1, 44.43, 4), new("Sofia", 23.32, 42.7, 3), new("Belgrade", 20.46, 44.8, 3),
        new("Zagreb", 15.98, 45.81, 3), new("Ljubljana", 14.5, 46.05, 2), new("Sarajevo", 18.41, 43.86, 2), new("Skopje", 21.43, 42.0, 2), new("Tirana", 19.82, 41.33, 2),
        new("Thessaloniki", 22.94, 40.64, 3), new("Bratislava", 17.11, 48.15, 2), new("Odesa", 30.73, 46.48, 3), new("Kharkiv", 36.23, 49.99, 4), new("Dnipro", 35.05, 48.46, 3),
        new("Lviv", 24.03, 49.84, 3), new("Chisinau", 28.86, 47.01, 2), new("Rotterdam", 4.48, 51.92, 3), new("Antwerp", 4.4, 51.22, 3), new("Luxembourg", 6.13, 49.61, 1),
        new("Bergen", 5.32, 60.39, 2), new("Gothenburg", 11.97, 57.71, 3), new("Malmö", 13.0, 55.6, 2), new("Aarhus", 10.2, 56.15, 2), new("Cardiff", -3.18, 51.48, 2),
        new("Belfast", -5.93, 54.6, 2), new("Leeds", -1.55, 53.8, 3), new("Liverpool", -2.98, 53.41, 3), new("Newcastle", -1.61, 54.97, 2), new("Bristol", -2.59, 51.45, 2),
        new("Cork", -8.47, 51.9, 2), new("Bilbao", -2.93, 43.26, 2), new("Zaragoza", -0.88, 41.65, 2), new("Malaga", -4.42, 36.72, 2), new("Palermo", 13.36, 38.12, 3),
        new("Bari", 16.87, 41.12, 2), new("Genoa", 8.93, 44.41, 2), new("Bologna", 11.34, 44.49, 2), new("Nice", 7.26, 43.7, 2), new("Strasbourg", 7.75, 48.58, 2),
        new("Ankara", 32.86, 39.93, 4), new("Izmir", 27.14, 38.42, 3), new("Cairo", 31.24, 30.04, 5), new("Alexandria", 29.92, 31.2, 4), new("Tunis", 10.18, 36.8, 3),
        new("Algiers", 3.06, 36.75, 4), new("Casablanca", -7.59, 33.57, 4), new("Rabat", -6.84, 34.02, 3), new("Tripoli", 13.19, 32.89, 3), new("Tel Aviv", 34.78, 32.08, 3),
        new("Beirut", 35.5, 33.89, 3), new("Damascus", 36.29, 33.51, 4), new("Baghdad", 44.36, 33.31, 5), new("Tehran", 51.39, 35.69, 5), new("Volgograd", 44.5, 48.7, 3),
        new("Kazan", 49.1, 55.8, 3), new("Nizhny Novgorod", 44.0, 56.3, 3), new("Rostov", 39.7, 47.2, 3), new("Tbilisi", 44.8, 41.7, 3), new("Baku", 49.87, 40.4, 3),
        new("Yerevan", 44.5, 40.18, 2), new("Reykjavik", -21.9, 64.15, 2), new("Trondheim", 10.4, 63.43, 2), new("Tampere", 23.76, 61.5, 2), new("Murmansk", 33.08, 68.97, 2),
        new("Arkhangelsk", 40.52, 64.54, 2), new("Smolensk", 32.05, 54.78, 2), new("Voronezh", 39.2, 51.66, 3), new("Samara", 50.1, 53.2, 3), new("Konya", 32.48, 37.87, 2),
        new("Antalya", 30.71, 36.9, 2), new("Valletta", 14.51, 35.9, 1), new("Nicosia", 33.38, 35.17, 1), new("Palma", 2.65, 39.57, 1), new("Cagliari", 9.11, 39.22, 1),
    ];

    // ---------- In map parcels (computed once) ----------
    static readonly (string name, double w, (double x, double y)[] pts)[] RiverPts =
        Rivers.Select(r => (r.Name, r.Width, Pts(r.Path))).ToArray();
    static readonly (string name, double w, double peak, bool forest, (double x, double y)[] pts)[] RangePts =
        Ranges.Select(r => (r.Name, r.Width * NationMap.ParcelsPerDegree, r.Peak, r.Forest, Pts(r.Ridge))).ToArray();
    static readonly (string name, double x, double y, double r, int size)[] CityPts =
        Cities.Select(c => { var (x, y) = NationMap.ToMap(c.Lon, c.Lat); return (c.Name, x, y, 1.5 + c.Size * c.Size * 0.4, c.Size); }).ToArray();

    static (double x, double y)[] Pts(double[] lonLat) =>
        Enumerable.Range(0, lonLat.Length / 2).Select(k => NationMap.ToMap(lonLat[2 * k], lonLat[2 * k + 1])).ToArray();

    static double DistToPath((double x, double y)[] p, double x, double y)
    {
        if (p.Length == 1) return Math.Sqrt((x - p[0].x) * (x - p[0].x) + (y - p[0].y) * (y - p[0].y));
        var best = double.MaxValue;
        for (var k = 1; k < p.Length; k++)
        {
            var (ax, ay) = p[k - 1]; var (bx, by) = p[k];
            double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            var t = len2 == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / len2, 0, 1);
            double ex = ax + t * dx - x, ey = ay + t * dy - y;
            best = Math.Min(best, ex * ex + ey * ey);
        }
        return Math.Sqrt(best);
    }

    /// <summary>How strongly an elliptical area (in degrees) covers this parcel: 1 at its centre, fading to 0 at its edge.</summary>
    static double Cover(double lon, double lat, double rx, double ry, double x, double y)
    {
        var (cx, cy) = NationMap.ToMap(lon, lat);
        double ax = rx * NationMap.ParcelsPerDegree * NationMap.CosLat, ay = ry * NationMap.ParcelsPerDegree;
        var d = Math.Sqrt((x - cx) * (x - cx) / (ax * ax) + (y - cy) * (y - cy) / (ay * ay));
        return Math.Clamp(1.2 - d, 0, 1);
    }

    /// <summary>Is this parcel part of a river or lake?</summary>
    public static bool Water(double x, double y)
    {
        foreach (var (_, w, pts) in RiverPts) if (DistToPath(pts, x, y) < w * 0.75) return true;
        foreach (var l in Lakes) if (Cover(l.Lon, l.Lat, l.Rx, l.Ry, x, y) > 0.2) return true;
        return false;
    }

    /// <summary>Height of the land, 0 (plains) to 9 (the high Alps).</summary>
    public static int Elevation(double x, double y)
    {
        double e = 0;
        foreach (var (_, w, peak, _, pts) in RangePts)
        {
            var d = DistToPath(pts, x, y) / w;
            e = Math.Max(e, peak * Math.Exp(-d * d * 1.6));
        }
        if (e > 0.5) e += (SectorTemplateNoise(x * 0.35, y * 0.35) - 0.5) * 2.2;
        return (int)Math.Clamp(Math.Round(e), 0, 9);
    }

    static double SectorTemplateNoise(double x, double y) => SectorTemplate.Noise(x, y);
    static double NWO_ss(double a, double b, double x) { var t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

    /// <summary>How much of each kind of land this spot has, from its real geography. Whatever is left stays barren.</summary>
    public static Dictionary<string, double> Mix(double x, double y, int elevation)
    {
        var lonlat = NationMap.ToLonLat(x, y);
        var m = new Dictionary<string, double> { ["wa"] = .02, ["ro"] = .03, ["oi"] = .01, ["fe"] = .07, ["gr"] = .12, ["sc"] = .08, ["ru"] = .02 };
        var e = elevation;
        // Hills are wooded; the high mountains are bare rock
        double forest = 0;
        foreach (var (_, w, peak, isForest, pts) in RangePts)
        {
            var d = DistToPath(pts, x, y) / w;
            if (isForest) forest = Math.Max(forest, Math.Exp(-d * d * 1.2));
        }
        m["sc"] += forest * 0.32;
        m["ro"] += e >= 7 ? 0.45 : e * 0.035;
        m["fe"] *= Math.Max(0.2, 1 - e * 0.12); m["gr"] *= Math.Max(0.3, 1 - e * 0.08);
        // The north German plain: open grass and farmland
        if (lonlat.lat > 52.3 && lonlat.lat < 56) { m["gr"] += .05; m["fe"] += .03; }
        // Climate across the region: boreal forest and tundra in the north, dry Mediterranean south, steppe to the east, desert below the Atlas
        var (lon, lat) = lonlat;
        if (lat > 58) { m["sc"] += NWO_ss(58, 62, lat) * 0.25; m["fe"] *= 1 - NWO_ss(58, 64, lat) * 0.8; }
        if (lat > 66) { m["sc"] *= 0.5; m["ro"] += 0.12; m["gr"] *= 0.6; }
        if (lat < 42 && lat > 33) { m["gr"] *= 0.6; m["fe"] *= 0.8; m["sc"] *= 0.7; m["ro"] += 0.04; }
        if (lon > 32 && lat > 45 && lat < 53) { m["gr"] += 0.12; m["fe"] += 0.06; m["sc"] *= 0.5; }
        if (lat < 33.5) { var dry = NWO_ss(33.5, 31, lat); m["gr"] *= 1 - dry * 0.9; m["fe"] *= 1 - dry * 0.9; m["sc"] *= 1 - dry; m["oi"] += dry * 0.12; m["ro"] += dry * 0.05; }
        foreach (var a in Areas)
        {
            var c = Cover(a.Lon, a.Lat, a.Rx, a.Ry, x, y);
            if (c <= 0) continue;
            switch (a.Kind)
            {
                case "farm": m["fe"] += .22 * c; m["gr"] += .08 * c; break;
                case "forest": m["sc"] += .28 * c; break;
                case "heath": m["sc"] += .15 * c; m["gr"] += .08 * c; m["fe"] *= 1 - .6 * c; break;
                case "lakes": m["wa"] += .10 * c; break;
                case "oil": m["oi"] += .30 * c; break;
                case "city": m["ru"] += .30 * c; break;
            }
        }
        // The old cities, now ruins
        foreach (var (_, cx, cy, r, size) in CityPts)
        {
            var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d < r * 1.6) m["ru"] += 0.85 * Math.Clamp(1.4 - d / r, 0, 1);
        }
        var sum = m.Values.Sum();
        if (sum > 0.9) foreach (var k in m.Keys.ToList()) m[k] *= 0.9 / sum;
        return m;
    }

    static HashSet<(int x, int y)>? roads;
    /// <summary>The old road network: each city joined to its nearest neighbours, and every sector hall joined to the nearest road.
    /// Roads cross rivers on bridges.</summary>
    public static HashSet<(int x, int y)> Roads => roads ??= BuildRoads();
    static HashSet<(int x, int y)> BuildRoads()
    {
        var set = new HashSet<(int x, int y)>();
        var edges = new HashSet<(int a, int b)>();
        for (var a = 0; a < CityPts.Length; a++)
        {
            var near = Enumerable.Range(0, CityPts.Length).Where(b => b != a)
                .OrderBy(b => Dist(CityPts[a], CityPts[b])).Take(CityPts[a].size >= 3 ? 3 : 2)
                .Where(b => Dist(CityPts[a], CityPts[b]) < (CityPts[a].size >= 3 ? 140 : 75));
            foreach (var b in near) edges.Add((Math.Min(a, b), Math.Max(a, b)));
        }
        foreach (var (a, b) in edges) Line(set, CityPts[a].x, CityPts[a].y, CityPts[b].x, CityPts[b].y);
        // Every sector hall gets a road to the network
        foreach (var n in NationMap.Sectors)
        {
            var (gx, gy) = NationMap.Origin(n);
            int hx = gx + SectorTemplate.Hall.i, hy = gy + SectorTemplate.Hall.j;
            (int x, int y) best = default; var bd = double.MaxValue;
            foreach (var c in set) { var d = (c.x - hx) * (c.x - hx) + (c.y - hy) * (c.y - hy); if (d < bd) { bd = d; best = c; } }
            if (bd < double.MaxValue) Line(set, hx + 0.5, hy + 0.5, best.x + 0.5, best.y + 0.5);
        }
        return set;
    }
    static double Dist((string, double x, double y, double, int) a, (string, double x, double y, double, int) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    /// <summary>A road one parcel wide along a straight line, with no diagonal gaps.</summary>
    static void Line(HashSet<(int x, int y)> set, double x0, double y0, double x1, double y1)
    {
        int cx = (int)Math.Floor(x0), cy = (int)Math.Floor(y0), tx = (int)Math.Floor(x1), ty = (int)Math.Floor(y1);
        set.Add((cx, cy));
        var steps = 0;
        while ((cx != tx || cy != ty) && steps++ < 2000)
        {
            // step along whichever axis keeps us closest to the true line
            double dx = x1 - x0, dy = y1 - y0, len = Math.Sqrt(dx * dx + dy * dy);
            int sx = Math.Sign(tx - cx), sy = Math.Sign(ty - cy);
            double Err(int px, int py) => Math.Abs((px + 0.5 - x0) * dy - (py + 0.5 - y0) * dx) / Math.Max(1e-9, len);
            if (sx != 0 && (sy == 0 || Err(cx + sx, cy) <= Err(cx, cy + sy))) cx += sx; else cy += sy;
            set.Add((cx, cy));
        }
    }

    /// <summary>Where the old towns stood on the map (no names), so the client can draw their ruins: position and size 1-5.</summary>
    public static IEnumerable<object> TownSpots() => CityPts.Select(c => new { x = Math.Round(c.x, 1), y = Math.Round(c.y, 1), size = c.size });
}
