using Azure.AI.OpenAI;
using DBChatPro.Models;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

namespace DBChatPro.Services
{
    // Use this constructor if you're using vanilla OpenAI instead of Azure OpenAI
    // Make sure to update your Program.cs as well
    //public class OpenAIService(OpenAIClient aiClient)

    public class OpenAIService(AzureOpenAIClient aiClient)
    {
        public async Task<AIQuery> GetAISQLQuery(string userPrompt, DatabaseSchema dbSchema)
        {
            var deploymentName = "gpt-4o";
            ChatClient chatClient = aiClient.GetChatClient(deploymentName);
            
            List<ChatMessage> chatHistory = new List<ChatMessage>();
            var builder = new StringBuilder();

            builder.AppendLine("Your are a helpful, cheerful database assistant. Do not respond with any information unrelated to databases or queries. Use the following database schema when creating your answers:");

            foreach(var table in dbSchema.SchemaRaw)
            {
                builder.AppendLine(table);
            }

            builder.AppendLine("Include column name headers in the query results.");
            builder.AppendLine("Always provide your answer in the JSON format below:");
            builder.AppendLine(@"{ ""summary"": ""your-summary"", ""query"":  ""your-query"" }");
            builder.AppendLine("Output ONLY JSON formatted on a single line. Do not use new line characters.");
            builder.AppendLine(@"In the preceding JSON response, substitute ""your-query"" with Microsoft SQL Server Query to retrieve the requested data.");
            builder.AppendLine(@"In the preceding JSON response, substitute ""your-summary"" with an explanation of each step you took to create this query in a detailed paragraph.");
            builder.AppendLine("Do not use MySQL syntax.");
            builder.AppendLine("Always limit the SQL Query to 100 rows.");
            builder.AppendLine("Always include all of the table columns and details.");

            // Build the AI chat/prompts
            chatHistory.Add(new SystemChatMessage(builder.ToString()));
            chatHistory.Add(new UserChatMessage(userPrompt));

            // Send request to Azure OpenAI model
            var response = await chatClient.CompleteChatAsync(chatHistory);
            var responseContent = response.Value.Content[0].Text.Replace("```json", "").Replace("```", "").Replace("\\n", "");

            try
            {
                return JsonSerializer.Deserialize<AIQuery>(responseContent);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse AI response as a SQL Query. The AI response was: " + response.Value.Content[0].Text);
            }
        }

        public async Task<ChatCompletion> ChatPrompt(List<ChatMessage> prompt)
        {
            var deploymentName = "gpt-4o";
            ChatClient chatClient = aiClient.GetChatClient(deploymentName);

            return (await chatClient.CompleteChatAsync(prompt)).Value;
        }
    }
}
