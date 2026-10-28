using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Tests.Common;

public class ResultTests
{
    private static readonly Error Taken = new("user.username_taken", "Ce nom d'utilisateur existe déjà.");

    [Fact]
    public void Success_HasNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        Result result = Taken;

        Assert.False(result.IsSuccess);
        Assert.Equal("user.username_taken", result.Error.Code);
    }

    [Fact]
    public void GenericSuccess_ExposesTheValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenReadingTheValue()
    {
        Result<int> result = Taken;

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
