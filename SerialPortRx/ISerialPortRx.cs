namespace CP.IO.Ports
{
    using System;
    using System.IO.Ports;
    using Reactive.Bindings;

    /// <summary>
    /// Serial Port Rx interface
    /// </summary>
    public interface ISerialPortRx : IDisposable
    {
        /// <summary>
        /// Gets or sets the baud rate.
        /// </summary>
        /// <value>The baud rate.</value>
        int BaudRate { get; set; }

        /// <summary>
        /// Gets or sets the data bits.
        /// </summary>
        /// <value>The data bits.</value>
        int DataBits { get; set; }

        /// <summary>
        /// Gets the data received.
        /// </summary>
        /// <value>The data received.</value>
        IObservable<char> DataReceived { get; }

        /// <summary>
        /// Gets the error recived.
        /// </summary>
        /// <value>The error recived.</value>
        IObservable<Exception> ErrorReceived { get; }

        /// <summary>
        /// Gets or sets the handshake.
        /// </summary>
        /// <value>The handshake.</value>
        Handshake Handshake { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value><c>true</c> if this instance is disposed; otherwise, <c>false</c>.</value>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the is open.
        /// </summary>
        /// <value>The is open.</value>
        IReadOnlyReactiveProperty<bool> IsOpen { get; }

        /// <summary>
        /// Gets or sets the parity.
        /// </summary>
        /// <value>The parity.</value>
        Parity Parity { get; set; }

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        /// <value>The port.</value>
        string PortName { get; set; }

        /// <summary>
        /// Gets or sets the read timeout.
        /// </summary>
        /// <value>The read timeout.</value>
        int ReadTimeout { get; set; }

        /// <summary>
        /// Gets or sets the stop bits.
        /// </summary>
        /// <value>The stop bits.</value>
        StopBits StopBits { get; set; }

        /// <summary>
        /// Gets or sets the write timeout.
        /// </summary>
        /// <value>The write timeout.</value>
        int WriteTimeout { get; set; }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        void Close();

        /// <summary>
        /// Opens this instance.
        /// </summary>
        void Open();

        /// <summary>
        /// Writes the specified byte array.
        /// </summary>
        /// <param name="byteArray">The byte array.</param>
        void Write(byte[] byteArray);

        /// <summary>
        /// Writes the specified text.
        /// </summary>
        /// <param name="text">The text.</param>
        void Write(string text);

        /// <summary>
        /// Writes the specified character array.
        /// </summary>
        /// <param name="charArray">The character array.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        void Write(char[] charArray, int offset, int count);

        /// <summary>
        /// Writes the specified character array.
        /// </summary>
        /// <param name="charArray">The character array.</param>
        void Write(char[] charArray);

        /// <summary>
        /// Writes the specified byte array.
        /// </summary>
        /// <param name="byteArray">The byte array.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        void Write(byte[] byteArray, int offset, int count);

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="text">The text.</param>
        void WriteLine(string text);
    }
}