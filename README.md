# OpenAiRag - AI Chat API with OpenAI Assistants

**OpenAiRag** is an ASP.NET Core 9 Web API that implements a Retrieval-Augmented Generation (RAG) system using the OpenAI Assistants API. It enables AI-powered conversations with context retention through conversation threads and supports tool calling (function calling) for dynamic interactions, such as fetching weather data. The API is documented with Swagger for easy testing.

## Features
- **AI-Powered Chat**: Interact with an OpenAI Assistant via a POST `/api/Agent/Chat` endpoint.
- **Conversation Context**: Uses threads to maintain conversation history, with each thread representing a unique conversation.
- **Streaming Support**: Stream responses in real-time using the `/api/Agent/ChatStream` endpoint.
- **Tool Calling**: Supports function calls (e.g., `GetWeatherInCelcius`) configured in the OpenAI Assistants dashboard.
- **Swagger Integration**: Explore and test the API using Swagger UI on `http://localhost:7294/swagger`.

## Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- An [OpenAI account](https://platform.openai.com/) with access to the Assistants API
- An OpenAI Assistant created via the OpenAI dashboard
- An OpenAI API key
- Git (for cloning the repository)

## Installation
1. **Clone the Repository**:
   ```bash
   git clone https://github.com/shahzaib-pk/OpenAiRag.git
   cd OpenAiRag
   ```

2. **Restore Dependencies**:
   ```bash
   dotnet restore
   ```

3. **Configure OpenAI Assistant**:
   - Log in to the [OpenAI Platform](https://platform.openai.com/).
   - Navigate to the Assistants section and create a new Assistant.
   - Configure the Assistant’s settings, including any tool-calling functions (e.g., `GetWeatherInCelcius`).
     - For `GetWeatherInCelcius`, define the function in the OpenAI dashboard with a `city` parameter (e.g., `{"city": "string"}`).
   - Copy the **Assistant ID** from the dashboard.
   - Generate an **API Key** from your OpenAI account settings.

4. **Update Configuration**:
   - Open `Controllers/AgentController.cs`.
   - Replace the placeholder values for `_assistantId` and `_apiKey`:
     ```csharp
     private const string _assistantId = "your-assistant-id-here";
     private const string _apiKey = "your-api-key-here";
     ```
   - **Note**: For production, store sensitive data like the API key in environment variables or a configuration file (e.g., `appsettings.json`) instead of hardcoding.

5. **Run the Application**:
   ```bash
   dotnet run
   ```
   - The API will start, and Swagger UI will be available at `http://localhost:7294/swagger` (or the port shown in the console).

## Usage
### Accessing the API
- Open Swagger UI at `http://localhost:7294/swagger` to test the endpoints interactively.
- Available endpoints:
  - **POST /api/Agent/Chat**: Send a chat message and receive a response with a thread ID for context.
    - Request body: `{ "promptMessage": "Hello, what's the weather in Karachi?", "threadId": null }`
    - Response: `{ "reply": "The weather in Karachi is 20C.", "threadId": "thread_abc123" }`
  - **POST /api/Agent/ChatStream**: Stream responses for real-time chat.
    - Request body: Same as above.
    - Response: Server-sent events with chunks of the AI’s response.

### Managing Conversations
- Each conversation is tied to a **thread**. The API returns a `threadId` in the response.
- To continue a conversation, include the `threadId` in subsequent requests:
  ```json
  {
    "promptMessage": "Tell me more about Karachi.",
    "threadId": "thread_abc123"
  }
  ```
- The Assistant remembers the conversation context within the same thread.

### Tool Calling
- The API supports tool calling, such as the `GetWeatherInCelcius` function, which returns mock weather data for cities (e.g., Karachi: 20C).
- Configure functions in the OpenAI Assistants dashboard:
  - Function name: `GetWeatherInCelcius`
  - Parameters: `{ "city": "string" }`
- The Assistant automatically calls the function when a user’s message matches the configured criteria (e.g., asking about weather).

## Project Structure
- **Controllers/AgentController.cs**: Handles chat and streaming endpoints, integrates with OpenAI Assistants API.
- **Dto/PromptDto.cs**: Defines the request model for chat messages.
- **LICENSE**: MIT License for open-source usage.

## Contributing
Contributions are welcome! Please read our [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact
For questions or feedback, open an issue on the [GitHub repository](https://github.com/shahzaib-pk/Rag_OpenAi_Assistants_DotNet_Core_9) or contact [shahzaibrind6@gmail.com](mailto:shahzaibrind6@gmail.com).

---

**Note**: This project uses the OpenAI Assistants API, which may incur costs depending on your OpenAI plan. Ensure you understand OpenAI’s pricing before deploying to production.
