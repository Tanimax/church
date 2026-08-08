using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Components.Pages.Admin;

namespace ChurchAttendance.Tests.Forms;

public class MemberFormModelTests
{
    private static List<ValidationResult> Validate(MemberFormModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    private static MemberFormModel ValidModel() => new()
    {
        FirstName = "Jean",
        LastName = "Dupont"
    };

    [Fact]
    public void Validate_MinimalValidModel_HasNoErrors()
    {
        var results = Validate(ValidModel());

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_MissingFirstName_ReportsError()
    {
        var model = ValidModel();
        model.FirstName = "";

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.FirstName)));
    }

    [Fact]
    public void Validate_MissingLastName_ReportsError()
    {
        var model = ValidModel();
        model.LastName = "   ";

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.LastName)));
    }

    [Fact]
    public void Validate_InvalidEmail_ReportsError()
    {
        var model = ValidModel();
        model.Email = "not-an-email";

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.Email)));
    }

    [Fact]
    public void Validate_InvalidPhone_ReportsError()
    {
        var model = ValidModel();
        model.Phone = "not-a-phone-number!!";

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.Phone)));
    }

    [Fact]
    public void Validate_EmptyStringPhone_HasNoErrors()
    {
        // Blazor's InputText posts "" for a blank optional field, never null —
        // this must be treated the same as "not provided".
        var model = ValidModel();
        model.Phone = "";

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_EmptyStringEmail_HasNoErrors()
    {
        var model = ValidModel();
        model.Email = "";

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_OnlyBirthDayProvided_ReportsError()
    {
        var model = ValidModel();
        model.BirthDay = 15;

        var results = Validate(model);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(MemberFormModel.BirthDay)) &&
            r.MemberNames.Contains(nameof(MemberFormModel.BirthMonth)));
    }

    [Fact]
    public void Validate_OnlyBirthMonthProvided_ReportsError()
    {
        var model = ValidModel();
        model.BirthMonth = 3;

        var results = Validate(model);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(MemberFormModel.BirthDay)) &&
            r.MemberNames.Contains(nameof(MemberFormModel.BirthMonth)));
    }

    [Fact]
    public void Validate_February30_ReportsError()
    {
        var model = ValidModel();
        model.BirthDay = 30;
        model.BirthMonth = 2;

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.BirthDay)));
    }

    [Fact]
    public void Validate_February29_IsAllowed()
    {
        var model = ValidModel();
        model.BirthDay = 29;
        model.BirthMonth = 2;

        var results = Validate(model);

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_April31_ReportsError()
    {
        var model = ValidModel();
        model.BirthDay = 31;
        model.BirthMonth = 4;

        var results = Validate(model);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(MemberFormModel.BirthDay)));
    }

    [Fact]
    public void Validate_ValidBirthDayAndMonth_HasNoErrors()
    {
        var model = ValidModel();
        model.BirthDay = 25;
        model.BirthMonth = 12;

        var results = Validate(model);

        Assert.Empty(results);
    }

}
