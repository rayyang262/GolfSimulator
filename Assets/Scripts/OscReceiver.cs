using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Lightweight UDP OSC receiver — no third-party packages required.
/// Attach to PlayerCapsule. Subscribe to OnMessage to receive parsed OSC data on the main thread.
///
/// OSC spec: http://opensoundcontrol.org/spec-1_0
/// TouchOSC sends accelerometer as: address="/accxyz"  type=",fff"  values=ax,ay,az (g-units)
/// </summary>
public class OscReceiver : MonoBehaviour
{
    [Header("OSC Settings")]
    [Tooltip("UDP port to listen on. Must match TouchOSC → Connections → Send Port.")]
    public int listenPort = 9000;

    /// <summary>
    /// Fired on the main thread (inside Update) for every received OSC message.
    /// Arg1 = OSC address string (e.g. "/accxyz")
    /// Arg2 = parsed float values (may be empty if message has no floats)
    /// </summary>
    public event Action<string, float[]> OnMessage;

    // ── private ──────────────────────────────────────────────────────────
    private UdpClient  _udp;
    private Thread     _thread;
    private volatile bool _running;

    // Thread-safe queue: background thread enqueues, Update dequeues on main thread
    private readonly ConcurrentQueue<(string address, float[] values)> _queue
        = new ConcurrentQueue<(string, float[])>();

    // ── lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        try
        {
            _udp = new UdpClient(listenPort);
            // Short receive timeout so the background thread can check _running and exit cleanly
            _udp.Client.ReceiveTimeout = 500;
            _running = true;
            _thread  = new Thread(ReceiveLoop) { IsBackground = true, Name = "OscReceiveThread" };
            _thread.Start();
            Debug.Log($"[OscReceiver] Listening for OSC on UDP port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[OscReceiver] Could not open UDP port {listenPort}: {e.Message}\n" +
                           "Check that no other app is using this port.");
        }
    }

    void Update()
    {
        // Drain the queue — fire all callbacks on the main thread this frame
        while (_queue.TryDequeue(out var msg))
            OnMessage?.Invoke(msg.address, msg.values);
    }

    void OnDestroy()
    {
        _running = false;
        _udp?.Close();
        _thread?.Join(1200);
    }

    // ── background receive loop ───────────────────────────────────────────

    void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref remote);
                HandlePacket(data, 0, data.Length);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                // Expected — the receive timeout fires so we can loop back and check _running
            }
            catch (ObjectDisposedException)
            {
                break; // Socket closed — normal shutdown
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[OscReceiver] Receive error: {e.Message}");
            }
        }
    }

    // ── OSC packet handling ───────────────────────────────────────────────

    /// <summary>
    /// Handles one UDP payload. Dispatches bundles recursively, plain messages directly.
    /// </summary>
    void HandlePacket(byte[] data, int start, int end)
    {
        if (end - start < 4) return;

        if (data[start] == (byte)'#')
        {
            // OSC bundle: "#bundle\0" (8 bytes) + timetag (8 bytes) = 16-byte header
            // followed by size-prefixed sub-messages
            int offset = start + 16;
            while (offset + 4 <= end)
            {
                int size = ReadInt32(data, offset); offset += 4;
                if (size > 0 && offset + size <= end)
                    HandlePacket(data, offset, offset + size);
                offset += size;
            }
        }
        else
        {
            HandleMessage(data, start, end);
        }
    }

    /// <summary>
    /// Parses a single OSC message and enqueues it for main-thread delivery.
    /// </summary>
    void HandleMessage(byte[] data, int start, int end)
    {
        int offset = start;

        // --- Address string (e.g. "/accxyz") ---
        string address = ReadOscString(data, ref offset, end);
        if (address == null || offset >= end) return;

        // --- Type tag string (e.g. ",fff") ---
        string typeTag = ReadOscString(data, ref offset, end);
        if (typeTag == null || typeTag.Length < 1 || typeTag[0] != ',') return;

        // --- Arguments (we extract floats; skip other types by advancing 4 bytes each) ---
        // Count floats first so we can allocate the right array size
        int floatCount = 0;
        for (int i = 1; i < typeTag.Length; i++)
            if (typeTag[i] == 'f') floatCount++;

        float[] values = new float[floatCount];
        int fi = 0;

        for (int i = 1; i < typeTag.Length; i++)
        {
            if (offset + 4 > end) break;

            if (typeTag[i] == 'f')
                values[fi++] = ReadFloat32(data, offset);
            // All basic OSC argument types are 4 bytes (float, int, bool pads to 4)
            // strings would need ReadOscString, but TouchOSC sensor data is always floats
            offset += 4;
        }

        _queue.Enqueue((address, values));
    }

    // ── binary helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Reads a null-terminated ASCII string and advances offset to the next 4-byte boundary.
    /// Returns null if the string cannot be read cleanly.
    /// </summary>
    static string ReadOscString(byte[] data, ref int offset, int end)
    {
        int strStart = offset;
        while (offset < end && data[offset] != 0) offset++;
        if (offset >= end) return null;

        string s = Encoding.ASCII.GetString(data, strStart, offset - strStart);
        offset++;                    // consume null terminator
        offset = Align4(offset);     // pad to 4-byte boundary
        return s;
    }

    /// <summary>Reads a big-endian 32-bit signed integer.</summary>
    static int ReadInt32(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    /// <summary>Reads a big-endian IEEE 754 single-precision float.</summary>
    static float ReadFloat32(byte[] data, int offset)
    {
        // OSC is big-endian; x86/ARM Windows/iOS is little-endian → swap bytes
        byte[] swapped = { data[offset + 3], data[offset + 2], data[offset + 1], data[offset] };
        return BitConverter.ToSingle(swapped, 0);
    }

    /// <summary>Round up to the nearest multiple of 4.</summary>
    static int Align4(int value) => (value + 3) & ~3;
}
