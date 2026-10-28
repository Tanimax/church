using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace ChurchAttendance.Architecture.Tests;

// Rules from docs/journalisation.md.
public class LoggingRulesTests
{
    private static readonly Regex ConsoleWrite = new(@"\bConsole\.(Error\.|Out\.)?Write(Line)?\s*\(", RegexOptions.Compiled);

    // logger.LogX($"...") or logger.LogX(ex, $"..."): the values are baked into the text, so the
    // log loses its searchable fields and every message is a different template.
    private static readonly Regex InterpolatedLogMessage = new(
        @"\.Log(Trace|Debug|Information|Warning|Error|Critical)?\s*\((\s*[\w.]+\s*,)*\s*\$@?""", RegexOptions.Compiled);

    [Fact]
    public void NoConsoleWrite_OnlyILogger()
    {
        var offenders = SourceCode.Files().Where(f => ConsoleWrite.IsMatch(f.Text)).Select(f => f.Path).ToList();

        Assert.True(offenders.Count == 0,
            "Console.Write au lieu d'ILogger (le niveau et le contexte sont perdus) : " + string.Join(", ", offenders));
    }

    [Fact]
    public void LogMessages_AreTemplates_NotInterpolatedStrings()
    {
        var offenders = SourceCode.Files().Where(f => InterpolatedLogMessage.IsMatch(f.Text)).Select(f => f.Path).ToList();

        Assert.True(offenders.Count == 0,
            "message de log interpolé ($\"...\") au lieu d'un modèle avec {Champs} : " + string.Join(", ", offenders));
    }

    // The domain stays pure: it returns results or throws, and the layers around it log.
    [Fact]
    public void Domain_DoesNotLog()
    {
        var result = Types.InAssembly(typeof(ChurchAttendance.Domain.Member).Assembly)
            .ShouldNot().HaveDependencyOn("Microsoft.Extensions.Logging")
            .GetResult();

        Assert.True(result.IsSuccessful, "Types du domaine qui journalisent : " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // Guards the regex itself, so the rule above can't pass by matching nothing.
    [Theory]
    [InlineData("logger.LogWarning($\"Échec pour {email}\");")]
    [InlineData("logger.LogError(ex, $\"Échec {id}\");")]
    [InlineData("_logger.Log(LogLevel.Information, $\"x {y}\");")]
    public void InterpolatedLogMessage_IsDetected(string line)
    {
        Assert.Matches(InterpolatedLogMessage, line);
    }

    [Theory]
    [InlineData("logger.LogWarning(\"Échec pour {Email}\", masked);")]
    [InlineData("logger.LogError(ex, \"{WhatFailed} (réf. {Reference})\", what, reference);")]
    public void TemplateLogMessage_IsAccepted(string line)
    {
        Assert.DoesNotMatch(InterpolatedLogMessage, line);
    }
}
