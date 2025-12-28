using System.Text.Json;
using Nupal.Domain.Entities;
using NUPAL.Core.Application.DTOs;
using NUPAL.Core.Application.Interfaces;

namespace NUPAL.Core.Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatConversationRepository _convoRepo;
        private readonly IChatMessageRepository _msgRepo;
        private readonly IStudentRepository _studentRepo;
        private readonly IRlRecommendationRepository _rlRepo;
        private readonly IAgentClient _agent;

        public ChatService(
            IChatConversationRepository convoRepo,
            IChatMessageRepository msgRepo,
            IStudentRepository studentRepo,
            IRlRecommendationRepository rlRepo,
            IAgentClient agent)
        {
            _convoRepo = convoRepo;
            _msgRepo = msgRepo;
            _studentRepo = studentRepo;
            _rlRepo = rlRepo;
            _agent = agent;
        }

        public async Task<ChatSendResponseDto> SendAsync(string studentId, ChatSendRequestDto request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(studentId))
                throw new ArgumentException("studentId is required");

            if (request == null || string.IsNullOrWhiteSpace(request.Message))
                throw new ArgumentException("message is required");

            // 1) Resolve conversation
            ChatConversation? convo = null;
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                convo = await _convoRepo.GetByIdAsync(request.ConversationId);
                if (convo != null && convo.StudentId != studentId)
                {
                    // Prevent cross-user access
                    convo = null;
                }
            }

            if (convo == null)
            {
                convo = await _convoRepo.CreateAsync(new ChatConversation
                {
                    StudentId = studentId,
                    CreatedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                });
            }

            var convoId = convo.Id.ToString();

            // 2) Fetch history (oldest -> newest)
            var history = await _msgRepo.GetRecentByConversationAsync(convoId, limit: 30);
            var agentHistory = history
                .OrderBy(m => m.CreatedAt)
                .Select(m => new AgentHistoryMessageDto
                {
                    Role = m.Role,
                    Kind = m.Kind,
                    Content = m.Content
                })
                .ToList();

            // Add current message for routing (not yet persisted so we can tag it correctly)
            agentHistory.Add(new AgentHistoryMessageDto
            {
                Role = "user",
                Kind = "unknown",
                Content = request.Message.Trim()
            });

            // 4) Fetch latest RL recommendation snapshot (if present)
            AgentRlRecommendationDto? rlSnap = null;
            var student = await _studentRepo.GetByIdAsync(studentId);
            if (student != null)
            {
                RlRecommendation? rl = null;
                if (!string.IsNullOrWhiteSpace(student.LatestRecommendationId))
                {
                    rl = await _rlRepo.GetByIdAsync(student.LatestRecommendationId);
                    Console.WriteLine($"[ChatService] Found RL via LatestRecommendationId: {student.LatestRecommendationId}");
                }
                rl ??= await _rlRepo.GetLatestByStudentIdAsync(studentId);

                if (rl != null)
                {
                    rlSnap = new AgentRlRecommendationDto
                    {
                        TermIndex = rl.TermIndex,
                        Courses = rl.Courses ?? new List<string>(),
                        SlatesByTerm = rl.SlatesByTerm,
                        Metrics = rl.Metrics,
                        ModelVersion = rl.ModelVersion,
                        PolicyVersion = rl.PolicyVersion
                    };
                    Console.WriteLine($"[ChatService] Sending RL recommendation to agent: TermIndex={rl.TermIndex}, Courses={rl.Courses?.Count ?? 0}");
                }
                else
                {
                    Console.WriteLine($"[ChatService] No RL recommendation found for student: {studentId}");
                }
            }
            else
            {
                Console.WriteLine($"[ChatService] Student not found: {studentId}");
            }

            // 5) Route via agent
            var agentReq = new AgentRouteRequestDto
            {
                StudentId = studentId,
                Message = request.Message.Trim(),
                History = agentHistory,
                RlRecommendation = rlSnap
            };

            var agentResp = await _agent.RouteAsync(agentReq, ct);

            // 6) Persist the user message with the resolved kind
            var replyKinds = agentResp.Results.Select(r => r.Kind).Distinct().ToList();
            var userKind = replyKinds.Count switch
            {
                0 => agentResp.Intent == "recommendation" ? "rl" : "rag",
                1 => replyKinds[0],
                _ => "mixed"
            };

            await _msgRepo.CreateAsync(new ChatMessage
            {
                ConversationId = convoId,
                StudentId = studentId,
                Role = "user",
                Kind = userKind,
                Content = request.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            // 7) Persist assistant replies
            var replies = new List<ChatReplyDto>();
            foreach (var r in agentResp.Results)
            {
                var metadataJson = r.Metadata == null ? null : JsonSerializer.Serialize(r.Metadata);

                await _msgRepo.CreateAsync(new ChatMessage
                {
                    ConversationId = convoId,
                    StudentId = studentId,
                    Role = "assistant",
                    Kind = r.Kind,
                    Content = r.Answer,
                    MetadataJson = metadataJson,
                    CreatedAt = DateTime.UtcNow
                });

                replies.Add(new ChatReplyDto
                {
                    Kind = r.Kind,
                    Content = r.Answer,
                    MetadataJson = metadataJson
                });
            }

            await _convoRepo.TouchAsync(convoId);

            return new ChatSendResponseDto
            {
                ConversationId = convoId,
                Replies = replies
            };
        }
    }
}
