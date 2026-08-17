using KpicCafeteria.Application.Documents;
using KpicCafeteria.Documents.Templates;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 문서 양식 등록/활성화/삭제/기본 양식 시드 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\templates.py
/// </summary>
public class DocumentTemplateServiceTests
{
    private static byte[] DefaultBytes(string documentType)
        => DefaultTemplateResources.TryGetTemplateBytes(documentType)
            ?? throw new InvalidOperationException($"임베디드 기본 양식 없음: {documentType}");

    // =======================================================================
    // 등록
    // =======================================================================

    [Fact]
    public async Task Register_ValidTemplate_SavesWithVersionAndChecksum()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        var bytes = DefaultBytes("MEAL_PLAN");

        var template = await service.RegisterAsync("MEAL_PLAN", "테스트 양식", bytes, "test.hwpx", activate: true);

        Assert.Equal("MEAL_PLAN", template.DocumentType);
        Assert.Equal("테스트 양식", template.Name);
        Assert.Equal(1, template.Version);
        Assert.True(template.Active);
        Assert.True(template.IsValid);
        Assert.Equal(bytes.Length, template.FileSize);
        Assert.NotNull(template.ChecksumSha256);
        Assert.True(File.Exists(template.StoragePath));

        // 파일 내용 일치
        var stored = await File.ReadAllBytesAsync(template.StoragePath);
        Assert.Equal(bytes, stored);
    }

    [Fact]
    public async Task Register_UnsupportedType_Throws()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();

        await Assert.ThrowsAsync<UnsupportedDocumentTypeException>(() =>
            service.RegisterAsync("PURCHASE_ORDER", "발주서", DefaultBytes("MEAL_PLAN"), "po.hwpx", activate: true));
    }

    [Fact]
    public async Task Register_EmptyFile_Throws()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();

        await Assert.ThrowsAsync<DocumentException>(() =>
            service.RegisterAsync("MEAL_PLAN", "빈 양식", [], "empty.hwpx", activate: true));
    }

    [Fact]
    public async Task Register_InvalidFile_ThrowsAndDoesNotStore()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();

        await Assert.ThrowsAsync<KpicCafeteria.Documents.Hwpx.HwpxTemplateError>(() =>
            service.RegisterAsync("MEAL_PLAN", "손상 양식", [1, 2, 3], "broken.hwpx", activate: true));

        var templates = await service.ListAsync();
        Assert.Empty(templates);
    }

    // =======================================================================
    // 버전/활성화
    // =======================================================================

    [Fact]
    public async Task Register_Twice_IncrementsVersionAndDeactivatesPrevious()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        var bytes = DefaultBytes("MEAL_PLAN");

        var first = await service.RegisterAsync("MEAL_PLAN", "v1", bytes, "v1.hwpx", activate: true);
        var second = await service.RegisterAsync("MEAL_PLAN", "v2", bytes, "v2.hwpx", activate: true);

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.True(second.Active);

        var active = await service.FindActiveAsync("MEAL_PLAN");
        Assert.NotNull(active);
        Assert.Equal(second.Id, active!.Id);
        Assert.False((await service.ListAsync()).Single(x => x.Id == first.Id).Active);
    }

    [Fact]
    public async Task Activate_DeactivatesPreviousActive()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        var bytes = DefaultBytes("MEAL_PLAN");

        var first = await service.RegisterAsync("MEAL_PLAN", "v1", bytes, "v1.hwpx", activate: true);
        var second = await service.RegisterAsync("MEAL_PLAN", "v2", bytes, "v2.hwpx", activate: false);

        await service.ActivateAsync(second.Id);

        var active = await service.FindActiveAsync("MEAL_PLAN");
        Assert.Equal(second.Id, active!.Id);
        Assert.False((await service.ListAsync()).Single(x => x.Id == first.Id).Active);
    }

    [Fact]
    public async Task Activate_MissingTemplate_Throws()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();

        await Assert.ThrowsAsync<TemplateNotFoundException>(() => service.ActivateAsync(999));
    }

    // =======================================================================
    // 삭제
    // =======================================================================

    [Fact]
    public async Task Delete_ActiveTemplate_Throws()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        var template = await service.RegisterAsync("MEAL_PLAN", "활성", DefaultBytes("MEAL_PLAN"), "a.hwpx", activate: true);

        await Assert.ThrowsAsync<ActiveTemplateDeleteException>(() => service.DeleteAsync(template.Id));
    }

    [Fact]
    public async Task Delete_InactiveTemplate_RemovesRowAndFile()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        var bytes = DefaultBytes("MEAL_PLAN");
        await service.RegisterAsync("MEAL_PLAN", "활성", bytes, "a.hwpx", activate: true);
        var inactive = await service.RegisterAsync("MEAL_PLAN", "비활성", bytes, "b.hwpx", activate: false);

        await service.DeleteAsync(inactive.Id);

        var templates = await service.ListAsync();
        Assert.Single(templates);
        Assert.False(File.Exists(inactive.StoragePath));
    }

    // =======================================================================
    // 기본 양식
    // =======================================================================

    [Fact]
    public async Task SeedDefaults_NoActiveTemplate_RegistersAllTypes()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();

        await service.SeedDefaultsAsync();

        foreach (var documentType in DocumentTemplateService.ValidDocumentTypes)
        {
            var active = await service.FindActiveAsync(documentType);
            Assert.NotNull(active);
            Assert.True(active!.Active);
            Assert.True(active.IsValid);
        }
    }

    [Fact]
    public async Task SeedDefaults_ActiveTemplateExists_DoesNotDuplicate()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        await service.RegisterAsync("MEAL_PLAN", "사용자 양식", DefaultBytes("MEAL_PLAN"), "u.hwpx", activate: true);

        await service.SeedDefaultsAsync();

        var templates = await service.ListAsync();
        Assert.Single(templates, x => x.DocumentType == "MEAL_PLAN");
        Assert.Equal("사용자 양식", templates.Single(x => x.DocumentType == "MEAL_PLAN").Name);
    }

    [Fact]
    public async Task RestoreDefault_RegistersNewActiveVersion()
    {
        using var harness = new DocumentTestHarness();
        var service = harness.CreateTemplateService();
        await service.RegisterAsync("MEAL_PLAN", "사용자 양식", DefaultBytes("MEAL_PLAN"), "u.hwpx", activate: true);

        var restored = await service.RestoreDefaultAsync("MEAL_PLAN");

        Assert.Equal(2, restored.Version);
        Assert.True(restored.Active);
        var active = await service.FindActiveAsync("MEAL_PLAN");
        Assert.Equal(restored.Id, active!.Id);
    }
}
