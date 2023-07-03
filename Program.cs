// See https://aka.ms/new-console-template for more information

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Configuration;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Directory = System.IO.Directory;

const string firstReg = "(?<year>20[012][0-9])(?<month>[01][0-9])(?<day>[0-3][0-9])";
const string secondReg = "(?<day>[0-3][0-9])(?<month>[01][0-9])(?<year>20[012][0-9])";
ConfigurationObject configurationObject;

Regex[] allRegex = new Regex[]
{
    new Regex(firstReg, RegexOptions.Compiled),
    new Regex(secondReg, RegexOptions.Compiled),
};

void HandleFolder(string f)
{
    foreach (var ligne in Directory.EnumerateFiles(f))
    {
        HandleFile(ligne);
    }

    foreach (var ligne in Directory.EnumerateDirectories(f))
    {
        HandleFolder(ligne);
    }
}

DateTime? GetDateOfPicture(string file)
{
    IReadOnlyList<MetadataExtractor.Directory> metadata;
    try
    {
        metadata = ImageMetadataReader.ReadMetadata(file);
    }
    catch (Exception e)
    {
        Console.WriteLine($"Unable to retrieve Exif metadata from '{file}', {e.Message}");
        return null;
    }
    var exif = metadata.Where(a => a is ExifIfd0Directory || a is ExifSubIfdDirectory).ToList();
    int[] tagToTest = new int[] { ExifDirectoryBase.TagDateTime, ExifDirectoryBase.TagDateTimeDigitized, ExifDirectoryBase.TagDateTimeOriginal };
    foreach (var dico in exif)
    {
        foreach (var ligne in tagToTest)
        {
            var date = dico.TryGetDateTime(ligne, out var d);
            if (date)
            {
                return d;
            }
        }

    }
    return null;
}

void HandleFile(string file)
{

    string year = "", month = "";

    DateTime? testExif = GetDateOfPicture(file);

    if (testExif != null)
    {
        year = testExif.Value.Year.ToString();
        month = testExif.Value.Month.ToString();
    }
    else
    {
        Match? foundMatch = null;

        foreach (var ligne in allRegex)
        {
            string fName = Path.GetFileNameWithoutExtension(file);
            var test = ligne.Match(fName);
            if (test.Success)
            {
                foundMatch = test;
                break;
            }
        }

        if (foundMatch == null)
        {
            return;
        }

        var yearRegEx = foundMatch.Groups.Values.Where(a => a.Name == "year").First();
        var monthRegEx = foundMatch.Groups.Values.Where(a => a.Name == "month").First();

    }

    string targetPath = Path.Combine(configurationObject.TargetPath, year, month);

    if (Directory.Exists(targetPath) == false)
    {
        Directory.CreateDirectory(targetPath);
    }

    string targetFile = Path.Combine(targetPath, Path.GetFileName(file));
    if (File.Exists(targetFile))
    {
        Console.WriteLine("Deleting : " + file + " existing at " + targetFile);
        File.Delete(file);
    }
    else
    {
        Console.WriteLine("Moved : " + file);
        File.Move(file, targetFile);
    }

}

if (Environment.GetCommandLineArgs().Length == 1)
{
    Console.WriteLine("need the config file as first argument");
    return;
}

string configFile = Environment.GetCommandLineArgs().Last();
Console.WriteLine("Using config file : " + configFile);

IConfigurationBuilder builder = new ConfigurationBuilder();
builder.AddJsonFile(configFile);
IConfiguration configuration = builder.Build();

configurationObject = configuration.Get<ConfigurationObject>()!;
HandleFolder(configurationObject.SourceFiles);
