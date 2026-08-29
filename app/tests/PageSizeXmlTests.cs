using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class PageSizeXmlTests
    {
        /// <summary>
        /// Settings.Load fails closed: any unknown XML element or attribute is a hard
        /// error that silently falls back to defaults and throws away the user's
        /// configured page sizes. Serialization is by type shape rather than by
        /// assembly, so moving PageSize into ProFileCounter.Core should be inert -- this
        /// asserts that rather than assuming it, and will also catch a later property
        /// rename that would quietly discard everyone's settings.
        /// </summary>
        [Fact]
        public void PageSize_SerializesToTheExpectedElementNames()
        {
            var sizes = new List<PageSize>
            {
                new PageSize("ANSI-A [ 8.5 × 11 ]", 8m, 9m, 10m, 12m)
            };

            string xml;
            var serializer = new XmlSerializer(typeof(List<PageSize>));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, sizes);
                xml = writer.ToString();
            }

            Assert.Contains("<PageSize>", xml);
            Assert.Contains("<MinWidth>8</MinWidth>", xml);
            Assert.Contains("<MinHeight>10</MinHeight>", xml);
            Assert.Contains("<MaxWidth>9</MaxWidth>", xml);
            Assert.Contains("<MaxHeight>12</MaxHeight>", xml);
            Assert.Contains("<Name>ANSI-A [ 8.5 × 11 ]</Name>", xml);
            Assert.Contains("<Active>true</Active>", xml);
        }

        [Fact]
        public void PageSize_RoundTripsThroughXmlUnchanged()
        {
            var original = new PageSize("ARCH-D [ 24 × 36 ]", 23m, 25m, 35m, 37m, active: false);

            var serializer = new XmlSerializer(typeof(PageSize));
            string xml;
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, original);
                xml = writer.ToString();
            }

            PageSize restored;
            using (var reader = new StringReader(xml))
            {
                restored = (PageSize)serializer.Deserialize(reader);
            }

            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.MinWidth, restored.MinWidth);
            Assert.Equal(original.MaxWidth, restored.MaxWidth);
            Assert.Equal(original.MinHeight, restored.MinHeight);
            Assert.Equal(original.MaxHeight, restored.MaxHeight);
            Assert.Equal(original.Active, restored.Active);
        }
    }
}
