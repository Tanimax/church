using ChurchAttendance.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;

namespace ChurchAttendance.Infrastructure.Cards;

// The card font and banner ship in wwwroot, hence IWebHostEnvironment (see WebRootFiles).
internal sealed class MemberCardRenderer(IWebHostEnvironment env) : IMemberCardRenderer
{
    public byte[] RenderZip(IEnumerable<(string FullName, string CardUrl)> members) =>
        MemberCardZipService.BuildZip(members, env.WebRootFileProvider);
}
