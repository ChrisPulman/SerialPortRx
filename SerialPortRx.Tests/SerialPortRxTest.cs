namespace SerialPortRx.Tests
{
    using System;
    using CP.IO.Ports;
    using NUnit.Framework;

    [TestFixture]
    [Timeout(10000)]
    public class SerialPortRxTest
    {
        [Test]
        [Category("SerialPortRx")]
        public void SimpleConstructor()
        {
            var src = new SerialPortRx();
            src.Dispose();
            Assert.That(src.IsDisposed, Is.True);
        }

        [Test]
        [Category("SerialPortRx")]
        public void SimpleConstructorWithPort()
        {
            var src = new SerialPortRx("COM1");
            Assert.That(src.PortName, Is.EqualTo("COM1"));
            src.Dispose();
            Assert.That(src.IsDisposed, Is.True);
        }

        [Test]
        [Category("SerialPortRx")]
        public void SimpleConstructorWithPortandBaud()
        {
            var src = new SerialPortRx("COM1", 9600);
            Assert.That(src.PortName, Is.EqualTo("COM1"));
            Assert.That(src.BaudRate, Is.EqualTo(9600));
            src.Dispose();
            Assert.That(src.IsDisposed, Is.True);
        }

        [Test]
        [Category("SerialPortRx")]
        public void SimpleConstructorWithPortandBaudAndDatabits()
        {
            var src = new SerialPortRx("COM1", 9600, 8);
            Assert.That(src.PortName, Is.EqualTo("COM1"));
            Assert.That(src.BaudRate, Is.EqualTo(9600));
            Assert.That(src.DataBits, Is.EqualTo(8));
            src.Dispose();
            Assert.That(src.IsDisposed, Is.True);
        }
    }
}
