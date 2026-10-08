using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SearchAChurch.Api.Data;
using SearchAChurch.Api.Data.Entities;
using SearchAChurch.Api.Features.Claim.Gateways;
using SearchAChurch.Api.Features.Claim.Services;

namespace SearchAChurch.UnitTests.Features.Claim;

[Trait("Category", "Unit")]
public class DisputeResolutionEngineTests
{
    private class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public TestTimeProvider(DateTimeOffset initialUtcNow) => _utcNow = initialUtcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DisputeTestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static Church CreateTestChurch(
        AppDbContext context,
        Guid? id = null,
        string name = "Igreja Teste",
        ChurchClaimState state = ChurchClaimState.Verified,
        VerificationTier tier = VerificationTier.Tier1_Cartorio,
        Guid? verifiedByUserId = null)
    {
        var church = new Church
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            FormattedAddress = "Av. Paulista, 1000",
            Latitude = -23.56,
            Longitude = -46.65,
            ClaimStatus = state,
            VerificationTier = tier,
            VerifiedByUserId = verifiedByUserId,
            IsVerified = state == ChurchClaimState.Verified
        };
        context.Churches.Add(church);
        return church;
    }

    private static User CreateTestUser(AppDbContext context, Guid? id = null, string name = "Pastor Teste")
    {
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Email = $"{Guid.NewGuid():N}@igreja.com.br",
            PasswordHash = "hash123",
            Role = UserRole.ChurchRep
        };
        context.Users.Add(user);
        return user;
    }

    private readonly Mock<IClaimAuditLogService> _auditLogServiceMock = new();
    private readonly Mock<IQsaValidationGateway> _qsaGatewayMock = new();
    private readonly Mock<IClaimNotificationService> _notificationServiceMock = new();
    private readonly TestTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero)); // Quinta-feira

    private readonly ClaimConnectionMetadata _testConnection = new(
        ClientIp: "200.180.10.5",
        ClientPort: 44321,
        UserAgent: "Mozilla/5.0 Android",
        TimestampUtc: new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero)
    );

    public DisputeResolutionEngineTests()
    {
        // Setup QSA default to valid
        _qsaGatewayMock.Setup(q => q.IsValidCpf(It.IsAny<string>())).Returns(true);
        _qsaGatewayMock.Setup(q => q.IsValidCnpj(It.IsAny<string>())).Returns(true);
    }

    private DisputeResolutionEngine CreateEngine(AppDbContext dbContext)
    {
        return new DisputeResolutionEngine(
            dbContext,
            _auditLogServiceMock.Object,
            _qsaGatewayMock.Object,
            _notificationServiceMock.Object,
            NullLogger<DisputeResolutionEngine>.Instance,
            _timeProvider
        );
    }

    #region 1. Validações e Casos de Borda (ProcessContestAsync)

    [Fact]
    public async Task ProcessContestAsync_WhenTosNotAccepted_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: false,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Diretoria eleita"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TERMOS_NAO_ACEITOS");
    }

    [Theory]
    [InlineData(VerificationTier.None)]
    [InlineData(VerificationTier.Tier2_Institucional)]
    [InlineData(VerificationTier.Tier3_SocialPresencial)]
    public async Task ProcessContestAsync_WhenSubmittedTierIsNotTier1_ReturnsFailure(VerificationTier nonTier1)
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: nonTier1,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("NIVEL_PROBATORIO_INSUFICIENTE");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenDocumentHashMissing_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "   ",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DOCUMENTO_RCPJ_OBRIGATORIO");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenCpfIsInvalid_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);
        _qsaGatewayMock.Setup(q => q.IsValidCpf("000.000.000-00")).Returns(false);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "000.000.000-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CPF_INVALIDO");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenCnpjIsInvalid_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);
        _qsaGatewayMock.Setup(q => q.IsValidCnpj("11.111.111/1111-11")).Returns(false);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "11.111.111/1111-11",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CNPJ_INVALIDO");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenChurchNotFound_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("IGREJA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenChurchAlreadyInDispute_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(church.Id, Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPUTA_EM_ANDAMENTO");
    }

    [Theory]
    [InlineData(ChurchClaimState.Unclaimed)]
    [InlineData(ChurchClaimState.Pending_Verification)]
    public async Task ProcessContestAsync_WhenChurchIsNotVerified_ReturnsFailure(ChurchClaimState nonVerifiedState)
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: nonVerifiedState, tier: VerificationTier.None);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(church.Id, Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CONTESTACAO_APENAS_PERFIS_VERIFICADOS");
    }

    [Fact]
    public async Task ProcessContestAsync_WhenChallengerIsAlreadyTheVerifiedOwner_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var church = CreateTestChurch(context, state: ChurchClaimState.Verified, tier: VerificationTier.Tier2_Institucional, verifiedByUserId: challenger.Id);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Carlos",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_rcpj_123",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Contestação"
        );

        var result = await engine.ProcessContestAsync(church.Id, challenger.Id, request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_JA_E_TITULAR");
    }

    #endregion

    #region 2. Resolução Automática N1 sobre Níveis 2 e 3 (CLAIM-10, AD-012)

    [Theory]
    [InlineData(VerificationTier.Tier3_SocialPresencial)]
    [InlineData(VerificationTier.Tier2_Institucional)]
    public async Task ProcessContestAsync_WhenChurchVerifiedTier2Or3_ExecutesAutomaticOverrideN1_RevokesPriorAndTransfersOwnership(
        VerificationTier incumbentTier)
    {
        using var context = CreateInMemoryDbContext();
        var incumbentUser = CreateTestUser(context, name: "Pastor Incumbente");
        var challengerUser = CreateTestUser(context, name: "Pastor Desafiante");

        var church = CreateTestChurch(context, name: "Igreja Batista Fonte", state: ChurchClaimState.Verified, tier: incumbentTier, verifiedByUserId: incumbentUser.Id);

        var priorClaim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = incumbentUser.Id,
            Status = ClaimRecordStatus.Approved,
            TargetTier = incumbentTier,
            ValidationMethod = ValidationMethod.SocialBio,
            TosAccepted = true,
            TosVersion = "1.0"
        };
        context.ChurchClaims.Add(priorClaim);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor André Presidente",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_sha256_ata_posse_rcpj",
            DocumentAverbationDate: _timeProvider.GetUtcNow(),
            Justification: "Ata de Posse registrada em RCPJ"
        );

        // Act
        var result = await engine.ProcessContestAsync(church.Id, challengerUser.Id, request, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Success.Should().BeTrue();
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.AutomaticOverrideN1);
        result.Value.DisputeId.Should().BeNull(); // Sem litígio aberto; resolvido na hora!
        result.Value.CurrentTier.Should().Be(VerificationTier.Tier1_Cartorio);
        result.Value.ActiveRepresentativeUserId.Should().Be(challengerUser.Id);

        // Validar banco de dados
        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        updatedChurch.VerificationTier.Should().Be(VerificationTier.Tier1_Cartorio);
        updatedChurch.VerifiedByUserId.Should().Be(challengerUser.Id);
        updatedChurch.IsVerified.Should().BeTrue();

        // Validar que claim anterior foi revogado
        var updatedPriorClaim = await context.ChurchClaims.FindAsync(priorClaim.Id);
        updatedPriorClaim!.Status.Should().Be(ClaimRecordStatus.Revoked);

        // Validar que novo claim foi criado e aprovado para o challenger
        var newClaim = await context.ChurchClaims
            .Include(c => c.Evidences)
            .FirstOrDefaultAsync(c => c.ChurchId == church.Id && c.UserId == challengerUser.Id);
        newClaim.Should().NotBeNull();
        newClaim!.Status.Should().Be(ClaimRecordStatus.Approved);
        newClaim.TargetTier.Should().Be(VerificationTier.Tier1_Cartorio);
        newClaim.Evidences.Should().HaveCount(1);
        newClaim.Evidences.First().FileHashSha256.Should().Be("hash_sha256_ata_posse_rcpj");

        // Validar logs de auditoria (Marco Civil art. 15)
        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimRevoked",
            church.Id,
            incumbentUser.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "ClaimApprovedAutomaticOverride",
            church.Id,
            challengerUser.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Validar notificação ao titular anterior informando que não cabe bloqueio unilateral (CLAIM-10)
        _notificationServiceMock.Verify(n => n.NotifyRevocationAsync(
            incumbentUser.Id,
            church.Id,
            church.Name,
            It.Is<string>(msg => msg.Contains("não cabe retenção ou bloqueio unilateral")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 3. Conflito Paritário N1 vs N1 e Congelamento In_Dispute (CLAIM-11, AD-016)

    [Fact]
    public async Task ProcessContestAsync_WhenChurchVerifiedTier1_OpensParityDispute_TransitionsToInDisputeAndCalculates5BusinessDays()
    {
        using var context = CreateInMemoryDbContext();
        var incumbentUser = CreateTestUser(context, name: "Pastor Incumbente");
        var challengerUser = CreateTestUser(context, name: "Pastor Desafiante");

        var church = CreateTestChurch(context, name: "Primeira Igreja Presbiteriana", state: ChurchClaimState.Verified, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbentUser.Id);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeContestRequest(
            TosVersion: "1.0",
            TosAccepted: true,
            LegalRepresentativeName: "Pastor Desafiante",
            LegalRepresentativeCpf: "123.456.789-00",
            ChurchCnpj: "12.345.678/0001-90",
            SubmittedTier: VerificationTier.Tier1_Cartorio,
            DocumentFileHash: "hash_ata_eleicao_2026",
            DocumentAverbationDate: _timeProvider.GetUtcNow().AddDays(-5),
            Justification: "Nova diretoria eleita em assembleia extraordinária"
        );

        // Act
        var result = await engine.ProcessContestAsync(church.Id, challengerUser.Id, request, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ResolutionType.Should().Be(DisputeResolutionType.ParityDisputeOpened);
        result.Value.DisputeId.Should().NotBeNull();
        result.Value.DeadlineAt.Should().NotBeNull();

        // Quinta-feira (08/10) + 5 dias úteis:
        // Sexta (1), [Sáb, Dom], Seg (2), Ter (3), Quarta (4), Quinta (5) -> 15/10/2026 10:00 UTC
        var expectedDeadline = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
        result.Value.DeadlineAt.Should().Be(expectedDeadline);

        // Validar congregação em In_Dispute
        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.In_Dispute);

        // Validar DisputeCase persistido
        var dispute = await context.DisputeCases.FindAsync(result.Value.DisputeId!.Value);
        dispute.Should().NotBeNull();
        dispute!.ChurchId.Should().Be(church.Id);
        dispute.ChallengerUserId.Should().Be(challengerUser.Id);
        dispute.IncumbentUserId.Should().Be(incumbentUser.Id);
        dispute.Status.Should().Be(DisputeStatus.Open);
        dispute.DeadlineAt.Should().Be(expectedDeadline);
        dispute.ChallengerDocumentHash.Should().Be("hash_ata_eleicao_2026");

        // Validar auditoria de abertura da disputa
        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "DisputeOpened",
            church.Id,
            challengerUser.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Validar notificações para ambas as partes
        _notificationServiceMock.Verify(n => n.NotifyDisputeOpenedAsync(
            challengerUser.Id, church.Id, church.Name, dispute.Id, expectedDeadline, true, It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyDisputeOpenedAsync(
            incumbentUser.Id, church.Id, church.Name, dispute.Id, expectedDeadline, false, It.IsAny<CancellationToken>()), Times.Once);

        // Validar perfil congelado (CLAIM-11)
        var isFrozen = await engine.IsProfileFrozenAsync(church.Id);
        isFrozen.Should().BeTrue();
    }

    #endregion

    #region 4. Cálculo de Dias Úteis (CalculateBusinessDays)

    [Fact]
    public void CalculateBusinessDays_WhenStartingOnDifferentDaysOfWeek_CalculatesExactly5WorkingDays()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        // 1. Início na Segunda-feira 10:00 -> Termina na próxima Segunda-feira 10:00 (Ter, Qua, Qui, Sex, Seg)
        var monday = new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero);
        var mondayDeadline = engine.CalculateBusinessDays(monday, 5);
        mondayDeadline.Should().Be(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));

        // 2. Início na Quarta-feira 15:00 -> Termina na próxima Quarta-feira 15:00 (Qui, Sex, Seg, Ter, Qua)
        var wednesday = new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);
        var wednesdayDeadline = engine.CalculateBusinessDays(wednesday, 5);
        wednesdayDeadline.Should().Be(new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero));

        // 3. Início na Sexta-feira 18:00 -> Termina na próxima Sexta-feira 18:00 (Seg, Ter, Qua, Qui, Sex)
        var friday = new DateTimeOffset(2026, 10, 9, 18, 0, 0, TimeSpan.Zero);
        var fridayDeadline = engine.CalculateBusinessDays(friday, 5);
        fridayDeadline.Should().Be(new DateTimeOffset(2026, 10, 16, 18, 0, 0, TimeSpan.Zero));

        // 4. Início no Sábado -> Pula fim de semana e adiciona 5 dias úteis (Seg, Ter, Qua, Qui, Sex)
        var saturday = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var saturdayDeadline = engine.CalculateBusinessDays(saturday, 5);
        saturdayDeadline.Should().Be(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

        // 5. Início no Domingo -> Pula e adiciona 5 dias úteis (Seg, Ter, Qua, Qui, Sex)
        var sunday = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var sundayDeadline = engine.CalculateBusinessDays(sunday, 5);
        sundayDeadline.Should().Be(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

        // 6. Zero ou negativo -> Retorna a própria data
        engine.CalculateBusinessDays(monday, 0).Should().Be(monday);
    }

    #endregion

    #region 5. Anexação de Provas na Janela de Litígio (SubmitDisputeEvidenceAsync)

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenDocumentHashIsEmpty_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeEvidenceSubmissionRequest("", _timeProvider.GetUtcNow());
        var result = await engine.SubmitDisputeEvidenceAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("HASH_DOCUMENTO_OBRIGATORIO");
    }

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenDisputeNotFound_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var request = new DisputeEvidenceSubmissionRequest("hash_123", _timeProvider.GetUtcNow());
        var result = await engine.SubmitDisputeEvidenceAsync(Guid.NewGuid(), Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPUTA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenDisputeIsClosed_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute);
        var challenger = CreateTestUser(context);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            Status = DisputeStatus.ResolvedChallenger
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeEvidenceSubmissionRequest("hash_123", _timeProvider.GetUtcNow());
        var result = await engine.SubmitDisputeEvidenceAsync(dispute.Id, challenger.Id, request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPUTA_JA_ENCERRADA");
    }

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenUserIsNotChallengerOrIncumbent_ReturnsFailure()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute);
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3)
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeEvidenceSubmissionRequest("hash_123", _timeProvider.GetUtcNow());
        var result = await engine.SubmitDisputeEvidenceAsync(dispute.Id, Guid.NewGuid(), request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USUARIO_NAO_AUTORIZADO");
    }

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenDeadlinePassed_ReturnsFailurePrazoPrecluso()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute);
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddHours(-1) // Prazo venceu há 1h
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var request = new DisputeEvidenceSubmissionRequest("hash_123", _timeProvider.GetUtcNow());
        var result = await engine.SubmitDisputeEvidenceAsync(dispute.Id, challenger.Id, request, _testConnection);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("PRAZO_PRECLUSO");
    }

    [Fact]
    public async Task SubmitDisputeEvidenceAsync_WhenIncumbentSubmitsWithinDeadline_SavesEvidenceAndAudits()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute);
        var incumbent = CreateTestUser(context);
        var challenger = CreateTestUser(context);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3)
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var averbationDate = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);
        var request = new DisputeEvidenceSubmissionRequest("hash_certidao_incumbente", averbationDate, "Certidão atualizada do RCPJ 2º Ofício");

        // Act
        var result = await engine.SubmitDisputeEvidenceAsync(dispute.Id, incumbent.Id, request, _testConnection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentFileHash.Should().Be("hash_certidao_incumbente");
        result.Value.AverbationDate.Should().Be(averbationDate);

        var updatedDispute = await context.DisputeCases.FindAsync(dispute.Id);
        updatedDispute!.IncumbentDocumentHash.Should().Be("hash_certidao_incumbente");
        updatedDispute.IncumbentAverbationDate.Should().Be(averbationDate);
        updatedDispute.ResolutionNotes.Should().Contain("Certidão atualizada do RCPJ 2º Ofício");

        _auditLogServiceMock.Verify(a => a.RecordEventAsync(
            "DisputeEvidenceSubmitted",
            dispute.ChurchId,
            incumbent.Id,
            _testConnection,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 6. Resolução por Prevalência Registral (CLAIM-12, Cenário 15)

    [Fact]
    public async Task ResolveParityDisputeAsync_PrevalenciaRegistral_WhenChallengerHasNewerAverbation_ChallengerWinsAndChurchVerified()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context, name: "Pastor Desafiante");
        var incumbent = CreateTestUser(context, name: "Pastor Incumbente");

        var church = CreateTestChurch(context, name: "Igreja Evangélica Renovada", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var priorClaim = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = incumbent.Id,
            Status = ClaimRecordStatus.Approved,
            TargetTier = VerificationTier.Tier1_Cartorio,
            ValidationMethod = ValidationMethod.CartorioRcpj
        };
        context.ChurchClaims.Add(priorClaim);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3),
            ChallengerDocumentHash = "hash_challenger",
            ChallengerAverbationDate = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), // Mais recente!
            IncumbentDocumentHash = "hash_incumbent",
            IncumbentAverbationDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero)   // Mais antigo
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.ResolvedChallenger);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.ResolvedByAverbationPrevalence);
        result.Value.WinnerUserId.Should().Be(challenger.Id);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Verified);
        result.Value.ChurchTier.Should().Be(VerificationTier.Tier1_Cartorio);

        // Validar banco de dados
        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        updatedChurch.VerifiedByUserId.Should().Be(challenger.Id);

        var updatedPriorClaim = await context.ChurchClaims.FindAsync(priorClaim.Id);
        updatedPriorClaim!.Status.Should().Be(ClaimRecordStatus.Revoked);

        var updatedDispute = await context.DisputeCases.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.ResolvedChallenger);
        updatedDispute.ResolvedAt.Should().NotBeNull();
        updatedDispute.ResolutionNotes.Should().Contain("Prevalência Registral: averbação do contestante");

        _notificationServiceMock.Verify(n => n.NotifyDisputeResolvedAsync(
            challenger.Id, church.Id, church.Name, dispute.Id, DisputeStatus.ResolvedChallenger, true, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyDisputeResolvedAsync(
            incumbent.Id, church.Id, church.Name, dispute.Id, DisputeStatus.ResolvedChallenger, false, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveParityDisputeAsync_PrevalenciaRegistral_WhenIncumbentHasNewerAverbation_IncumbentWinsAndChurchVerified()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var church = CreateTestChurch(context, name: "Igreja Evangélica Renovada", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3),
            ChallengerDocumentHash = "hash_challenger",
            ChallengerAverbationDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero),   // Mais antigo
            IncumbentDocumentHash = "hash_incumbent",
            IncumbentAverbationDate = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)    // Mais recente!
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.ResolvedIncumbent);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.ResolvedByAverbationPrevalence);
        result.Value.WinnerUserId.Should().Be(incumbent.Id);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Verified);

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Verified);
        updatedChurch.VerifiedByUserId.Should().Be(incumbent.Id);
    }

    [Fact]
    public async Task ResolveParityDisputeAsync_PrevalenciaRegistral_WhenAverbationDatesAreEqual_FallsBackToJudicialCancellation()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);
        var sameDate = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero);

        var church = CreateTestChurch(context, name: "Igreja Central em Conflito", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(2),
            ChallengerAverbationDate = sameDate,
            IncumbentAverbationDate = sameDate
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert: Dúvida insanável -> Anulado para via judicial, retorna para Unclaimed (CLAIM-12, Cenário 17)
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.CanceledJudicial);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.CanceledJudicialFallback);
        result.Value.WinnerUserId.Should().BeNull();
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        result.Value.ChurchTier.Should().Be(VerificationTier.None);

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        updatedChurch.VerifiedByUserId.Should().BeNull();
        updatedChurch.IsVerified.Should().BeFalse();
    }

    #endregion

    #region 7. Desclassificação Sumária por Inércia (CLAIM-12, Cenário 16)

    [Fact]
    public async Task ResolveParityDisputeAsync_Inertia_WhenChallengerProvidedAndIncumbentInert_ChallengerWins()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var church = CreateTestChurch(context, name: "Igreja Local", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(5),
            ChallengerDocumentHash = "hash_challenger_tempestivo",
            ChallengerAverbationDate = _timeProvider.GetUtcNow(),
            IncumbentDocumentHash = null, // Inerte!
            IncumbentAverbationDate = null
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Avança o tempo além do prazo fatal de 5 dias úteis
        _timeProvider.Advance(TimeSpan.FromDays(6));

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.ResolvedChallenger);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.ResolvedByInertia);
        result.Value.WinnerUserId.Should().Be(challenger.Id);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Verified);

        var updatedDispute = await context.DisputeCases.FindAsync(dispute.Id);
        updatedDispute!.ResolutionNotes.Should().Contain("Incumbente desclassificado sumariamente por inércia processual");
    }

    [Fact]
    public async Task ResolveParityDisputeAsync_Inertia_WhenIncumbentProvidedAndChallengerInert_IncumbentWins()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var church = CreateTestChurch(context, name: "Igreja Local", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(5),
            ChallengerDocumentHash = null, // Contestante inerte!
            ChallengerAverbationDate = null,
            IncumbentDocumentHash = "hash_incumbent_tempestivo",
            IncumbentAverbationDate = _timeProvider.GetUtcNow()
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Avança o tempo além do prazo de 5 dias úteis
        _timeProvider.Advance(TimeSpan.FromDays(6));

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.ResolvedIncumbent);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.ResolvedByInertia);
        result.Value.WinnerUserId.Should().Be(incumbent.Id);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Verified);

        var updatedDispute = await context.DisputeCases.FindAsync(dispute.Id);
        updatedDispute!.ResolutionNotes.Should().Contain("Contestante desclassificado sumariamente por inércia processual");
    }

    [Fact]
    public async Task ResolveParityDisputeAsync_Inertia_WhenBothPartiesInertAfterDeadline_FallsBackToJudicialCancellation()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var church = CreateTestChurch(context, name: "Igreja Abandonada", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(5),
            ChallengerDocumentHash = null, // Ambos inertes!
            IncumbentDocumentHash = null
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Avança tempo após o deadline
        _timeProvider.Advance(TimeSpan.FromDays(6));

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        // Assert: Ambos inertes -> Perfil retornado para Unclaimed
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.CanceledJudicial);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.CanceledJudicialFallback);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Unclaimed);

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        updatedChurch.VerifiedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task ResolveParityDisputeAsync_WhenDeadlineNotExpiredAndDocsNotSubmitted_ReturnsFailureDisputeInProgress()
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: ChurchClaimState.In_Dispute);
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3), // Ainda dentro do prazo
            ChallengerDocumentHash = "hash_c",
            IncumbentDocumentHash = null // Aguardando incumbente
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        var result = await engine.ResolveParityDisputeAsync(dispute.Id);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DISPUTA_EM_ANDAMENTO");
    }

    #endregion

    #region 8. Cancelamento Judicial Manual (CLAIM-12, Cenário 17)

    [Fact]
    public async Task ResolveParityDisputeAsync_ManualJudicialCancel_RevertsChurchToUnclaimedAndRevokesAllClaims()
    {
        using var context = CreateInMemoryDbContext();
        var challenger = CreateTestUser(context);
        var incumbent = CreateTestUser(context);

        var church = CreateTestChurch(context, name: "Igreja em Litígio Complexo", state: ChurchClaimState.In_Dispute, tier: VerificationTier.Tier1_Cartorio, verifiedByUserId: incumbent.Id);

        var claim1 = new ChurchClaim
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            UserId = incumbent.Id,
            Status = ClaimRecordStatus.Approved,
            TargetTier = VerificationTier.Tier1_Cartorio,
            ValidationMethod = ValidationMethod.CartorioRcpj
        };
        context.ChurchClaims.Add(claim1);

        var dispute = new DisputeCase
        {
            Id = Guid.NewGuid(),
            ChurchId = church.Id,
            ChallengerUserId = challenger.Id,
            IncumbentUserId = incumbent.Id,
            Status = DisputeStatus.Open,
            DeadlineAt = _timeProvider.GetUtcNow().AddDays(3)
        };
        context.DisputeCases.Add(dispute);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);

        // Act
        var result = await engine.ResolveParityDisputeAsync(dispute.Id, DisputeManualDecision.JudicialCancel);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DisputeStatus.CanceledJudicial);
        result.Value.ResolutionType.Should().Be(DisputeResolutionType.CanceledJudicialFallback);
        result.Value.ChurchClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        result.Value.ChurchTier.Should().Be(VerificationTier.None);
        result.Value.WinnerUserId.Should().BeNull();

        var updatedChurch = await context.Churches.FindAsync(church.Id);
        updatedChurch!.ClaimStatus.Should().Be(ChurchClaimState.Unclaimed);
        updatedChurch.VerificationTier.Should().Be(VerificationTier.None);
        updatedChurch.VerifiedByUserId.Should().BeNull();
        updatedChurch.IsVerified.Should().BeFalse();

        var updatedClaim = await context.ChurchClaims.FindAsync(claim1.Id);
        updatedClaim!.Status.Should().Be(ClaimRecordStatus.Revoked);

        _notificationServiceMock.Verify(n => n.NotifyJudicialCancellationAsync(
            challenger.Id, church.Id, church.Name, dispute.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(n => n.NotifyJudicialCancellationAsync(
            incumbent.Id, church.Id, church.Name, dispute.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 9. IsProfileFrozenAsync

    [Theory]
    [InlineData(ChurchClaimState.In_Dispute, true)]
    [InlineData(ChurchClaimState.Verified, false)]
    [InlineData(ChurchClaimState.Pending_Verification, false)]
    [InlineData(ChurchClaimState.Unclaimed, false)]
    public async Task IsProfileFrozenAsync_ReturnsExpectedResultBasedOnClaimStatus(ChurchClaimState state, bool expectedFrozen)
    {
        using var context = CreateInMemoryDbContext();
        var church = CreateTestChurch(context, state: state);
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var isFrozen = await engine.IsProfileFrozenAsync(church.Id);

        isFrozen.Should().Be(expectedFrozen);
    }

    [Fact]
    public async Task IsProfileFrozenAsync_WhenChurchDoesNotExist_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var engine = CreateEngine(context);

        var isFrozen = await engine.IsProfileFrozenAsync(Guid.NewGuid());
        isFrozen.Should().BeFalse();
    }

    #endregion
}
