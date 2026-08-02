using System.Net;
using SpotiSharpBackend;

namespace SpotiSharpBackend;

public class CallBackListener
{
    private static CallBackListener _callBackListener;
    public static CallBackListener Instance => _callBackListener ??= new CallBackListener();
    
    private HttpListener _httpListener = new HttpListener();

    private CallBackListener()
    {
        _httpListener.Prefixes.Add("http://127.0.0.1:5000/callback/");
        try
        {
            _httpListener.Start();
            DiagnosticLog.Write("[Auth] callback listener started on 127.0.0.1:5000");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Auth] callback listener failed to start: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        var _responseThread = new Thread(ResponseThread);
        _responseThread.Start();
    }

    ~CallBackListener()
    {
        if (_httpListener.IsListening)
        {
            _httpListener.Close();
        }
    }
    
    private void ResponseThread()
    {
        HttpListenerContext context;
        try
        {
            context = _httpListener.GetContext();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"[Auth] callback listener stopped waiting: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        var code = context.Request.QueryString["code"];
        DiagnosticLog.Write($"[Auth] callback request received, code present={!string.IsNullOrEmpty(code)}");

        byte[] _responseArray;
        if (!string.IsNullOrEmpty(code))
        {
            _responseArray = 
                """
                <html>
                    <head>
                        <title>
                            Authentication Successful
                        </title>
                    </head>
                    <body>
                        The Spotify Authentication was Successful. You can now close this page.
                    </body>
                </html>
            """u8.ToArray();
            
            Authentication.GetCallback(code);
        }
        else
        {
            _responseArray = 
                """
                <html>
                    <head>
                        <title>
                            Authentication Failed
                        </title>
                    </head>
                    <body>
                        The Spotify Authentication Failed.
                    </body>
                </html>
            """u8.ToArray();
        }
        context.Response.OutputStream.Write(_responseArray, 0, _responseArray.Length);
        context.Response.KeepAlive = false;
        context.Response.Close();
        _httpListener.Close();
        _callBackListener = null;
    }
}