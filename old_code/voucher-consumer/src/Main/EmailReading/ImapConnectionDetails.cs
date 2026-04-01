namespace Main.EmailReading;

public sealed record ImapConnectionDetails
{
   public ImapConnectionDetails(string username, string password, string server, int port, bool shouldUseSsl)
   {
      Username = username;
      Password = password;
      Server = server;
      Port = port;
      ShouldUseSSL = shouldUseSsl;
   }

   public string Username { get;  }
   public string Password { get;  }
   public string Server { get; }
   public int Port { get;  }
   public bool ShouldUseSSL { get;  }
}