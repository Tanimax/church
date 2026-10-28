using System.Text.RegularExpressions;

namespace ChurchAttendance.Architecture.Tests;

// Who may manage members is defined once, in AuthorizationPolicies.ManageMembers
// (src/Api/Startup/SecuritySetup.cs): pages and endpoints use the policy, never their own
// copy of the role list, so the API, the buttons and the handlers can't drift apart.
public class AuthorizationRulesTests
{
    // <AuthorizeView Roles="Admin,EcoleDominicale">, User.IsInRole(nameof(UserRole.EcoleDominicale)), RequireRole(...).
    private static readonly Regex HardCodedManagerRoles = new(
        @"(Roles\s*=\s*""[^""]*EcoleDominicale|IsInRole\([^)]*EcoleDominicale|RequireRole\([^)]*EcoleDominicale)",
        RegexOptions.Compiled);

    [Fact]
    public void MemberManagementRoles_AreOnlyListedInThePolicy()
    {
        var offenders = SourceCode.Files()
            .Where(f => f.Path != "src/Api/Startup/SecuritySetup.cs")
            .Where(f => HardCodedManagerRoles.IsMatch(f.Text))
            .Select(f => f.Path)
            .ToList();

        Assert.True(offenders.Count == 0,
            "liste de rôles École du dimanche codée en dur au lieu de AuthorizationPolicies.ManageMembers : "
            + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("<AuthorizeView Roles=\"Admin,EcoleDominicale\">")]
    [InlineData("user.IsInRole(nameof(UserRole.EcoleDominicale))")]
    public void HardCodedManagerRoles_IsDetected(string code)
    {
        Assert.Matches(HardCodedManagerRoles, code);
    }

    [Fact]
    public void RoleOptionInAForm_IsNotAHardCodedCheck()
    {
        Assert.DoesNotMatch(HardCodedManagerRoles, "<option value=\"EcoleDominicale\">École Dominicale</option>");
    }
}
