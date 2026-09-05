using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Elastic.Clients.Elasticsearch;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Ai;
using LexFlow.Infrastructure.Caching;
using LexFlow.Infrastructure.Comm;
using LexFlow.Infrastructure.Crm;
using LexFlow.Infrastructure.Dashboard;
using LexFlow.Infrastructure.Dms;
using LexFlow.Infrastructure.Fin;
using LexFlow.Infrastructure.Kb;
using LexFlow.Infrastructure.Legal;
using LexFlow.Infrastructure.Management;
using LexFlow.Infrastructure.Ops;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Persistence.Interceptors;
using LexFlow.Infrastructure.Portal;
using LexFlow.Infrastructure.Reporting;
using LexFlow.Infrastructure.Search;
using LexFlow.Infrastructure.Secrets;
using LexFlow.Infrastructure.Security;
using LexFlow.Infrastructure.Settings;
using LexFlow.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LexFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Registered even for non-web hosts (Workers/Runner) that reference Infrastructure
        // directly: AuditSaveChangesInterceptor depends on IHttpContextAccessor and simply
        // sees a null HttpContext outside a request, attributing the mutation to "system".
        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<LexFlowDbContext>((sp, options) =>
            options
                .UseNpgsql(configuration.GetConnectionString("LexFlowDatabase"))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LexFlowDbContext>());

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            return ConnectionMultiplexer.Connect(options.ConnectionString);
        });

        services.Configure<ElasticsearchOptions>(configuration.GetSection(ElasticsearchOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(ElasticsearchOptions.SectionName).Get<ElasticsearchOptions>()
                          ?? new ElasticsearchOptions();
            var settings = new ElasticsearchClientSettings(new Uri(options.Uri));
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                settings = settings.Authentication(new Elastic.Transport.ApiKey(options.ApiKey));
            }

            return new ElasticsearchClient(settings);
        });

        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>()
                          ?? new BlobStorageOptions();
            return new BlobServiceClient(options.ConnectionString);
        });

        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));
        var vaultUri = configuration.GetSection(KeyVaultOptions.SectionName)[nameof(KeyVaultOptions.VaultUri)];
        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            services.AddSingleton(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential()));
        }

        // --- Auth (PRD §20) ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtSigningKeyProvider>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IUserDenylistService, RedisUserDenylistService>();

        // --- Module 14: User Management ---
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ISessionManagementService, SessionManagementService>();

        // --- Module 15: Settings ---
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<INumberSeriesService, NumberSeriesService>();
        services.AddScoped<ITaxRateService, TaxRateService>();
        services.AddScoped<ICommTemplateService, CommTemplateService>();
        services.AddScoped<IGatewayConfigService, GatewayConfigService>();
        services.Configure<ExternalGatewayOptions>(configuration.GetSection(ExternalGatewayOptions.SectionName));
        services.AddHttpClient();
        services.AddScoped<ISmtpTestSender, SmtpTestSender>();
        services.AddScoped<ISmsTestSender, SmsTestSender>();
        services.AddScoped<IWhatsAppTestSender, WhatsAppTestSender>();
        services.AddScoped<IPaymentGatewayVerifier, PaymentGatewayVerifier>();

        // --- Module 2/3: CRM (Leads, Clients) ---
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IKycEncryptionService, KycEncryptionService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ILeadImportService, LeadImportService>();

        // --- Module 4/5: Legal (Matters, Court Cases, Hearings) ---
        services.AddScoped<IConflictCheckService, ConflictCheckService>();
        services.AddScoped<ILegalLookupService, LegalLookupService>();
        services.AddScoped<IOpsTaskService, OpsTaskService>();
        services.AddScoped<IMatterService, MatterService>();
        services.AddScoped<ICourtCaseService, CourtCaseService>();
        services.AddScoped<IHearingService, HearingService>();
        services.AddScoped<ICourtOrderService, CourtOrderService>();
        services.AddScoped<IEvidenceService, EvidenceService>();
        services.AddScoped<IWitnessService, WitnessService>();
        services.AddScoped<IArgumentNoteService, ArgumentNoteService>();

        // --- Module 7: DMS (Documents, Folders, Templates, e-Signature) ---
        services.Configure<ClamAvOptions>(configuration.GetSection(ClamAvOptions.SectionName));
        services.AddScoped<IAvScanner, ClamAvScanner>();
        services.AddSingleton(configuration.GetSection(TesseractOptions.SectionName).Get<TesseractOptions>() ?? new TesseractOptions());
        services.AddScoped<ITextExtractionService, TextExtractionService>();
        services.AddScoped<IDocumentIndexer, ElasticsearchDocumentIndexer>();
        services.AddScoped<IDocumentProcessingJobs, DocumentProcessingJobs>();
        services.AddScoped<DocumentIndexDispatchService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentShareLinkService, DocumentShareLinkService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.Configure<ESignatureOptions>(configuration.GetSection(ESignatureOptions.SectionName));
        services.AddScoped<ISignatureProvider, DocuSignSignatureProvider>();
        services.AddScoped<ISignatureProvider, AdobeSignSignatureProvider>();
        services.AddScoped<ISignatureService, SignatureService>();

        // --- Module 6/10, §22/§23: Ops (Calendar, Tasks, Notifications, Workflow Rules) ---
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.Configure<GoogleCalendarOptions>(configuration.GetSection(GoogleCalendarOptions.SectionName));
        services.Configure<MicrosoftGraphCalendarOptions>(configuration.GetSection(MicrosoftGraphCalendarOptions.SectionName));
        services.AddScoped<ICalendarSync, GoogleCalendarSyncService>();
        services.AddScoped<ICalendarSync, MicrosoftGraphCalendarSyncService>();
        services.AddScoped<IEmailNotificationProvider, NoopEmailNotificationProvider>();
        services.AddScoped<ISmsNotificationProvider, NoopSmsNotificationProvider>();
        services.AddScoped<IWhatsAppNotificationProvider, NoopWhatsAppNotificationProvider>();
        services.AddScoped<IPushNotificationProvider, NoopPushNotificationProvider>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IWorkflowEventPublisher, WorkflowEventPublisher>();
        services.AddScoped<WorkflowRuleEngine>();
        services.AddScoped<IWorkflowActionResumer>(sp => sp.GetRequiredService<WorkflowRuleEngine>());
        services.AddScoped<IWorkflowRuleService, WorkflowRuleService>();
        services.AddScoped<ReminderDispatchService>();
        services.AddScoped<TaskOverdueScanJob>();
        services.AddScoped<CalendarSyncPollingJob>();
        services.AddScoped<MaterializedViewRefreshJob>();
        services.AddScoped<PartitionMaintenanceJob>();
        services.AddScoped<AuditIntegrityCheckJob>();

        // --- Module 11, §34: Comm (Email, SMS, WhatsApp, Calls, Chat, Webhooks) ---
        services.AddScoped<GatewayCredentialResolver>();
        services.Configure<GmailOptions>(configuration.GetSection(GmailOptions.SectionName));
        services.Configure<MicrosoftGraphEmailOptions>(configuration.GetSection(MicrosoftGraphEmailOptions.SectionName));
        services.AddScoped<IInboundEmailHandler, InboundEmailHandler>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailSync, GmailSyncService>();
        services.AddScoped<IEmailSync, MicrosoftGraphEmailSyncService>();
        services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        services.AddScoped<ISmsProvider, Msg91SmsProvider>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IWhatsAppProvider, WhatsAppCloudApiProvider>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<ICallService, CallService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ICommTimelineService, CommTimelineService>();
        services.AddScoped<IWebhookRouter, WebhookRouter>();

        // --- Module 8/9: Fin (Time Tracking, Billing, Payments, Trust) ---
        services.AddScoped<ITimeTrackingService, TimeTrackingService>();
        services.AddScoped<IRateCardService, RateCardService>();
        services.AddScoped<IInvoicePdfRenderer, InvoicePdfRenderer>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<IPaymentGateway, RazorpayPaymentGateway>();
        services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();
        services.AddScoped<IDunningService, DunningService>();
        services.AddScoped<ITrustService, TrustService>();

        // --- Module 12: Knowledge Base (Acts, Judgments, Articles, Search, Taxonomy, Matter Pins) ---
        services.AddScoped<IKbActService, KbActService>();
        services.AddScoped<IKbJudgmentService, KbJudgmentService>();
        services.AddScoped<IKbArticleService, KbArticleService>();
        services.AddScoped<IKbTaxonomyService, KbTaxonomyService>();
        services.AddScoped<IKbMatterPinService, KbMatterPinService>();
        services.AddScoped<IKbSearchIndexer, ElasticsearchKbIndexer>();
        services.AddScoped<IKbSearchService, KbSearchService>();

        // --- Module 13: Reports & Analytics (standard/custom reports, scope injection, export, ETL) ---
        services.AddScoped<IReportScopeService, ReportScopeService>();
        services.AddScoped<IStandardReportService, StandardReportService>();
        services.AddScoped<ICustomReportService, CustomReportService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IReportRunService, ReportRunService>();
        services.AddScoped<IReportSchedulerService, ReportSchedulerService>();
        services.AddScoped<IReportingEtlService, ReportingEtlService>();

        // --- Module 16: AI Features (gateway, RAG, 12 features, quota, audit, transcription) ---
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddHttpClient(nameof(AnthropicLlmProvider), client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<ILlmProvider, AnthropicLlmProvider>();
        services.AddScoped<IEmbeddingProvider, HashingEmbeddingProvider>();
        services.AddScoped<ISpeechToTextService, NullSpeechToTextService>();
        services.AddSingleton<IAiPromptTemplateService, AiPromptTemplateService>();
        services.AddScoped<IAiRetrievalGuard, AiRetrievalGuard>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();
        services.AddScoped<ICitationVerifier, CitationVerifier>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IAiInteractionAuditService, AiInteractionAuditService>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddScoped<IAiTranscriptionService, AiTranscriptionService>();
        // Overridden by the Api process (SignalRJobsBroadcaster, registered after AddInfrastructure)
        // — see NullJobsBroadcaster's own doc comment for why Workers keeps this default.
        services.AddScoped<IJobsBroadcaster, NullJobsBroadcaster>();

        // --- Module 1: Dashboard ---
        services.AddScoped<IDashboardService, DashboardService>();

        // --- Module 17: Client Portal (separate identity realm, timeline, Pay-Now, uploads, appointments, messaging) ---
        services.AddScoped<IPortalScopeService, PortalScopeService>();
        services.AddScoped<IPortalAuthService, PortalAuthService>();
        services.AddScoped<IPortalTimelineService, PortalTimelineService>();
        services.AddScoped<IPortalInvoiceService, PortalInvoiceService>();
        services.AddScoped<IPortalPayNowService, PortalPayNowService>();
        services.AddScoped<IPortalDocumentService, PortalDocumentService>();
        services.AddScoped<IPortalAppointmentService, PortalAppointmentService>();
        services.AddScoped<IPortalMessagingService, PortalMessagingService>();

        return services;
    }
}
