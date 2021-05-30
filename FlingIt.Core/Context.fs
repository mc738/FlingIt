module FlingIt.Core.Context

open System
open System.Net
open System.Net.Mail
open System.Text.Json.Serialization
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open FLite.Core

[<CLIMutable>]
type Replacement =
    { [<JsonPropertyName("key")>]
      Key: string
      [<JsonPropertyName("value")>]
      Value: string }

[<CLIMutable>]
type MessageRequest =
    { [<JsonPropertyName("reference")>]
      Reference: Guid
      [<JsonPropertyName("subscription")>]
      Subscription: Guid
      [<JsonPropertyName("token")>]
      Token: string
      [<JsonPropertyName("template")>]
      Template: string
      [<JsonPropertyName("to")>]
      To: string list
      [<JsonPropertyName("cc")>]
      Cc: string list
      [<JsonPropertyName("bcc")>]
      Bcc: string list
      [<JsonPropertyName("attachments")>]
      Attachments: string list
      [<JsonPropertyName("replacements")>]
      Replacements: Replacement list }

[<AutoOpen>]
module Internal =

    let initializeAppSubscriptions = """
    CREATE TABLE app_subscriptions (
        reference TEXT NOT NULL,
	    name TEXT NOT NULL,
        token TEXT NOT NULL,
	    CONSTRAINT app_subscriptions_PK PRIMARY KEY (reference)
    );
    """

    let initializeTemplates = """
    CREATE TABLE templates (
	    reference TEXT NOT NULL,
        sub_reference TEXT NOT NULL,
        name TEXT NOT NULL,
        subject TEXT NOT NULL,
        template TEXT NOT NULL,
        from_name TEXT NOT NULL,
        created_on TEXT NOT NULL,
	    CONSTRAINT templates_PK PRIMARY KEY (reference),
        CONSTRAINT templates_FK FOREIGN KEY (sub_reference) REFERENCES app_subscriptions(reference)
    );
    """

    let initializeProfiles = """
    CREATE TABLE profiles (
	    reference TEXT NOT NULL,
        sub_reference TEXT NOT NULL UNIQUE,
        name TEXT NOT NULL,
        server TEXT NOT NULL,
        port NUMBER NOT NULL,
        username TEXT NOT NULL,
        password TEXT NOT NULL,
        enable_ssl NUMBER NOT NULL,
	    CONSTRAINT correlation_PK PRIMARY KEY (reference),
        CONSTRAINT profiles_FK FOREIGN KEY (sub_reference) REFERENCES app_subscriptions(reference)
    );
    """

    type Correlation =
        { Reference: Guid
          CreatedOn: DateTime
          SubReference: string }

    let initializeCorrelation = """
        CREATE TABLE correlation (
	        reference TEXT NOT NULL,
            created_on TEXT NOT NULL,
            sub_reference TEXT NOT NULL,
	        CONSTRAINT correlation_PK PRIMARY KEY (reference),
            CONSTRAINT correlation_FK FOREIGN KEY (sub_reference) REFERENCES app_subscriptions(reference)
        );
        """
        
    let saveCorrelations (qh: QueryHandler) (reference: Guid) (subReference: Guid) =
            qh.Insert("correlation", { Reference = reference; CreatedOn = DateTime.UtcNow; SubReference = subReference.ToString() })

    type Request =
        { Reference: Guid
          ReceivedOn: string
          RawBlob: BlobField }

    let initializeRequests = """
            CREATE TABLE requests (
	            reference TEXT NOT NULL UNIQUE,
                received_on TEXT NOT NULL,
                raw_blob BLOB NOT NULL,
	            CONSTRAINT requests_FK FOREIGN KEY (reference) REFERENCES correlation(reference)
            );
            """

    type RenderedMessage =
        { Reference: Guid
          CreatedOn: DateTime
          RawBlob: BlobField }

    let initializeRenderedMessages = """
            CREATE TABLE rendered_messages (
	            reference TEXT NOT NULL UNIQUE,
                created_on TEXT NOT NULL,
                raw_blob BLOB NOT NULL,
	            CONSTRAINT rendered_messages_FK FOREIGN KEY (reference) REFERENCES correlation(reference)
            );
            """

    type SendResult =
        { Reference: Guid
          AttemptedOn: DateTime
          Successful: bool }

    let initializeSendResults = """
            CREATE TABLE send_results (
	            reference TEXT NOT NULL UNIQUE,
                attempted_on TEXT NOT NULL,
                successful NUMBER NOT NULL,
     	        CONSTRAINT send_results_FK FOREIGN KEY (reference) REFERENCES correlation(reference)
            );
            """

    type InvalidRequest =
        { Reference: Guid
          Message: string
          CreatedOn: DateTime }

    let initializeInvalidRequests = """
            CREATE TABLE invalid_requests (
	            reference TEXT NOT NULL UNIQUE,
                message TEXT NOT NULL,
                created_on TEXT NOT NULL,
     	        CONSTRAINT errors_FK FOREIGN KEY (reference) REFERENCES correlation(reference)
            );
            """
            
    let saveInvalidRequest (qh: QueryHandler) (reference: Guid) (message: string) =
            qh.Insert("invalid_requests", { Reference = reference; Message = message; CreatedOn = DateTime.UtcNow })

    let initialize (qh: QueryHandler) =
        //log.LogInformation("Initialize database")
        qh.ExecuteSqlNonQuery(initializeAppSubscriptions)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeProfiles)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeTemplates)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeCorrelation)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeRequests)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeRenderedMessages)
        |> ignore

        qh.ExecuteSqlNonQuery(initializeSendResults)
        |> ignore
        
        qh.ExecuteSqlNonQuery(initializeInvalidRequests)
        |> ignore

    let getTemplateQuery = """
    SELECT
	    p.name as profile_name,
        p.server,
        p.port,
        p.username,
        p.password,
        p.enable_ssl,
        t.name as template_name,
        t.subject,
        t.from_name,
        t.template
    FROM app_subscriptions a
    JOIN templates t ON a.reference = t.sub_reference
    JOIN profiles p ON a.reference = p.sub_reference
    WHERE a.reference = @sub_ref AND a.token = @app_token AND t.name = @template_name;
    """

    type GetTemplateQuery =
        { SubRef: string //TODO this should be a guid (possibly), FLite doesn't handle a guid the same as Guid.ToString() so string version used.
          AppToken: string
          TemplateName: string }

    type TemplateDetails =
        { ProfileName: string
          Server: string
          Port: int
          Username: string
          Password: string
          EnableSsl: bool
          TemplateName: string
          Subject: string
          FromName: string
          Template: string }

    let tryGetTemplate (qh: QueryHandler) subRef appToken templateName =

        let result =
            qh.SelectVerbatim<TemplateDetails, GetTemplateQuery>(
                getTemplateQuery,
                { SubRef = subRef
                  AppToken = appToken
                  TemplateName = templateName }
            )

        match result.Length > 0 with
        | true -> Some result.Head
        | false -> None

[<AutoOpen>]
module Templating =

    type ReplacementMap =
        { Map: Map<string, string>
          DelimiterStart: string
          DelimiterEnd: string }

        static member Create(map: Map<string, string>) =
            { Map = map
              DelimiterStart = "%"
              DelimiterEnd = "%" }

        member rm.Apply(input: string) =
            rm.Map
            |> Map.fold (fun (acc: string) k v -> acc.Replace($"{rm.DelimiterStart}{k}{rm.DelimiterEnd}", v)) input

module Email =

    let processMessage (client: SmtpClient) (request: MessageRequest) (details: TemplateDetails) body =
        //saveMessage qh command.Reference command.Message

        use message = new MailMessage()
        message.From <- MailAddress(details.FromName)
        message.Subject <- details.Subject
        message.IsBodyHtml <- true
        message.Body <- body
        request.To |> List.map message.To.Add |> ignore
        request.Cc |> List.map message.CC.Add |> ignore
        request.Bcc |> List.map message.Bcc.Add |> ignore

        request.Attachments
        |> List.map (fun a -> message.Attachments.Add(new Attachment(a)))
        |> ignore

        try
            client.Send(message)
            Ok()
        with ex -> Error $"Could not send email. Error: {ex.Message}"

    let sendEmail (logger: ILogger) (qh: QueryHandler) request (details: TemplateDetails) =
        let client = new SmtpClient(details.Server)
        client.Port <- details.Port
        client.Credentials <- NetworkCredential(details.Username, details.Password)
        client.EnableSsl <- details.EnableSsl

        let rm =
            ReplacementMap.Create(
                request.Replacements
                |> List.map (fun r -> r.Key, r.Value)
                |> Map.ofList
            )

        let body = rm.Apply(details.Template)

        // Save rendered message.

        match processMessage client request details body with
        | Ok _ -> logger.LogInformation($"Message '{request.Reference}' successfully send")
        | Error e -> logger.LogError($"Message '{request.Reference}' could not be sent. Error: {e}")

module Agent =

    let start qh (logger: ILogger) =

        let template = tryGetTemplate qh

        MailboxProcessor<MessageRequest>.Start
            (fun inbox ->
                let rec loop (count) =
                    async {
                        // Get the request.
                        //logger.LogInformation($"Awaiting request. Cycling: {count}")

                        let! request = inbox.TryReceive(10000)

                        match request with
                        | Some r ->
                            logger.LogInformation($"Request '{r.Reference}' received. Subscription: '{r.Subscription}'")
                            saveCorrelations qh r.Reference r.Subscription
                            
                            // Get the subscription and template.
                            
                      
                            match template (r.Subscription.ToString()) r.Token r.Template with
                            | Some t -> Email.sendEmail logger qh r t
                            | None ->
                                logger.LogError(
                                    "Subscription and/or template not found. Save request to `invalid_requests` table."
                                )
                                saveInvalidRequest qh r.Reference "Subscription and/or template not found."
                        | None ->
                            logger.LogInformation("No requests received")
                            // No requests received. Can do some house keeping if needed.
                            ()
                        return! loop (count + 1)
                    }

                loop (0))

type CommsContext(qh: QueryHandler, logger: ILogger<CommsContext>) =

    let agent = Agent.start qh logger
    
    static member Load(path, logger) = CommsContext(QueryHandler.Open path, logger)

    member _.Initialize() = initialize qh
    
    member _.QueueRequest(request: MessageRequest) = agent.Post request    
