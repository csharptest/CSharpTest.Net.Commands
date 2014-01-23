#region Copyright 2013 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
/* -------------------------------------------------------------------------------
 * DERIVED WORK FROM http://hpop.sourceforge.net/
 * Provided here with a compatible (Apache) license and a few modifications to 
 * limit functionality to MIME parsing only.  If desired, the original works are
 * available in public domain at the url above.
 * ---------------------------------------------------------------------------- */
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace CSharpTest.Net.Http
{
    /// <summary>
    /// Parses a raw HTML form where ContentType = "multipart/form-data", including one or more attachments.
    /// Caution advised as this is entirely in-memory for the moment and not a viable option for large files.
    /// </summary>
    public sealed class MimeMultiPartData : IEnumerable<MimeMessagePart>
    {
        private const long MaxMessageSize = 100*1024*1024;
        private readonly NameValueCollection _headers;
        private readonly MimeMessagePart _message;

        /// <summary>
        /// Constructs the form data from the input stream and http "Content-Type" header.
        /// </summary>
        public MimeMultiPartData(Stream input, string contentType)
            : this(input, ContentTypeHeaders(contentType))
        { }

        /// <summary>
        /// Constructs the form data from the input stream and the http headers.
        /// </summary>
        public MimeMultiPartData(Stream input, NameValueCollection headers)
        {
            _headers = new NameValueCollection(headers);
            using (MemoryStream outStream = new MemoryStream())
            {
                int bytesRead;
                byte[] buffer = new byte[16 * 1024];

                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    outStream.Write(buffer, 0, bytesRead);
                    if (outStream.Length > MaxMessageSize)
                        throw new ArgumentException("The input stream is too large.");
                }
                _message = new MimeMessagePart(outStream.ToArray(), _headers);
            }

            if (!_message.IsMultiPart)
                throw new ArgumentException("The provided content-type is not a multipart encoding or is not recognized.");
        }

        /// <summary>
        /// The headers originally provided.
        /// </summary>
        public NameValueCollection Headers { get { return _headers; } }

        /// <summary>
        /// The Content-Type header field.<br/>
        /// <br/>
        /// If not set, the ContentType is created by the default "text/plain; charset=us-ascii" which is
        /// defined in <a href="http://tools.ietf.org/html/rfc2045#section-5.2">RFC 2045 section 5.2</a>.
        /// </summary>
        public ContentType ContentType { get { return _message.ContentType; } }

        /// <summary>
        /// This header describes the Content encoding during transfer.<br/>
        /// <br/>
        /// If no Content-Transfer-Encoding header was present in the message, it is set
        /// to the default of ContentTransferEncoding.SevenBit in accordance to the RFC.
        /// </summary>
        public ContentTransferEncoding ContentTransferEncoding { get { return _message.ContentTransferEncoding; } }

        private static NameValueCollection ContentTypeHeaders(string contentType)
        {
            NameValueCollection c = new NameValueCollection();
            c["CONTENT-TYPE"] = contentType;
            return c;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<MimeMessagePart> GetEnumerator()
        {
            return _message.MessageParts.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _message.MessageParts.GetEnumerator();
        }

        /// <summary>
        /// Total number of message parts / Form data found.
        /// </summary>
        public int Count
        {
            get { return _message.MessageParts.Count; }
        }

        /// <summary>
        /// Returns a element by name, or ArgumentOutOfRangeException
        /// </summary>
        public MimeMessagePart this[string name]
        {
            get
            {
                MimeMessagePart p;
                if (TryGetMessagePart(name, out p))
                    return p;

                throw new ArgumentOutOfRangeException("The message part does not exist.", "name");
            }
        }

        /// <summary>
        /// Returns true if the element was found and the out parameter messagePart set successfully
        /// </summary>
        public bool TryGetMessagePart(string name, out MimeMessagePart messagePart)
        {
            foreach (MimeMessagePart part in _message.MessageParts)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(name, part.Name) || StringComparer.OrdinalIgnoreCase.Equals(name, part.FileName))
                {
                    messagePart = part;
                    return true;
                }
            }
            messagePart = null;
            return false;
        }

        /// <summary>
        /// Gets all parts for a given name, used in the case of multi-file upload controls.
        /// </summary>
        public IEnumerable<MimeMessagePart> GetAllPartsByName(string name)
        {
            foreach (MimeMessagePart part in _message.MessageParts)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(name, part.Name) || StringComparer.OrdinalIgnoreCase.Equals(name, part.FileName))
                {
                    yield return part;
                }
            }
        }

        /// <summary>
        /// Returns the unique names for all the message parts
        /// </summary>
        public ICollection<string> Keys 
        {
            get
            {
                Dictionary<string, string> keys = new Dictionary<string, string>();

                foreach (MimeMessagePart part in _message.MessageParts)
                    keys[part.Name] = part.Name;

                return keys.Keys;
            }
        }

        /// <summary>
        /// Gets all the parts that are not of type "text/plain" and/or have a filename value set in content disposition.
        /// </summary>
        public ICollection<MimeMessagePart> GetAttachments()
        {
            List<MimeMessagePart> result = new List<MimeMessagePart>();
            foreach (MimeMessagePart part in _message.MessageParts)
            {
                if (part.IsMultiPart || !StringComparer.OrdinalIgnoreCase.Equals(part.ContentType.MediaType, "text/plain") || part.FileName != part.Name)
                    result.Add(part);
            }
            return result;
        }

        /// <summary>
        /// Gets a dictionary of all name/value pairs of parts that have a content-type of "text/plain" and do not have 
        /// a filename value set in content disposition.
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (MimeMessagePart part in _message.MessageParts)
            {
                if (!part.IsMultiPart && StringComparer.OrdinalIgnoreCase.Equals(part.ContentType.MediaType, "text/plain") && part.FileName == part.Name)
                    result[part.Name] = part.Text;
            }
            return result;
        }
    }

    #region MessagePart
    /// <summary>
    /// A MessagePart is a part of an email message used to describe the whole email parse tree.
    /// </summary>
    public sealed class MimeMessagePart
    {
        private readonly MessageHeader _headers;
        private readonly List<MimeMessagePart> _messageParts;
        #region Public properties
        /// <summary>
        /// Returns the collection of headers for a message part
        /// </summary>
        public NameValueCollection Headers { get { return _headers.Headers; } }
        /// <summary>
        /// The Content-Type header field.<br/>
        /// <br/>
        /// If not set, the ContentType is created by the default "text/plain; charset=us-ascii" which is
        /// defined in <a href="http://tools.ietf.org/html/rfc2045#section-5.2">RFC 2045 section 5.2</a>.<br/>
        /// <br/>
        /// If set, the default is overridden.
        /// </summary>
        public ContentType ContentType { get { return _headers.ContentType; } }

        /// <summary>
        /// A human readable description of the body<br/>
        /// <br/>
        /// <see langword="null"/> if no Content-Description header was present in the message.<br/>
        /// </summary>
        public string ContentDescription { get { return _headers.ContentDescription; } }

        /// <summary>
        /// This header describes the Content encoding during transfer.<br/>
        /// <br/>
        /// If no Content-Transfer-Encoding header was present in the message, it is set
        /// to the default of ContentTransferEncoding.SevenBit in accordance to the RFC.
        /// </summary>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc2045#section-6">RFC 2045 section 6</a> for details</remarks>
        public ContentTransferEncoding ContentTransferEncoding { get { return _headers.ContentTransferEncoding; } }

        /// <summary>
        /// ID of the content part (like an attached image). Used with MultiPart messages.<br/>
        /// <br/>
        /// <see langword="null"/> if no Content-ID header field was present in the message.
        /// </summary>
        public string ContentId { get { return _headers.ContentId; } }

        /// <summary>
        /// Used to describe if a <see cref="MimeMessagePart"/> is to be displayed or to be though of as an attachment.<br/>
        /// Also contains information about filename if such was sent.<br/>
        /// <br/>
        /// <see langword="null"/> if no Content-Disposition header field was present in the message
        /// </summary>
        public ContentDisposition ContentDisposition { get { return _headers.ContentDisposition; } }

        /// <summary>
        /// This is the encoding used to parse the message body if the <see cref="MimeMessagePart"/><br/>
        /// is not a MultiPart message. It is derived from the <see cref="ContentType"/> character set property.
        /// </summary>
        public Encoding BodyEncoding { get; private set; }

        /// <summary>
        /// This is the parsed body of this <see cref="MimeMessagePart"/>.<br/>
        /// It is parsed in that way, if the body was ContentTransferEncoded, it has been decoded to the
        /// correct bytes.<br/>
        /// <br/>
        /// It will be <see langword="null"/> if this <see cref="MimeMessagePart"/> is a MultiPart message.<br/>
        /// Use <see cref="IsMultiPart"/> to check if this <see cref="MimeMessagePart"/> is a MultiPart message.
        /// </summary>
        public byte[] Body { get; private set; }

        /// <summary>
        /// Describes if this <see cref="MimeMessagePart"/> is a MultiPart message<br/>
        /// <br/>
        /// The <see cref="MimeMessagePart"/> is a MultiPart message if the <see cref="ContentType"/> media type property starts with "multipart/"
        /// </summary>
        public bool IsMultiPart
        {
            get
            {
                return ContentType.MediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// A <see cref="MimeMessagePart"/> is considered to be holding text in it's body if the MediaType
        /// starts either "text/" or is equal to "message/rfc822"
        /// </summary>
        public bool IsText
        {
            get
            {
                string mediaType = ContentType.MediaType;
                return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// A <see cref="MimeMessagePart"/> is considered to be an attachment, if<br/>
        /// - it is not holding <see cref="IsText">text</see> and is not a <see cref="IsMultiPart">MultiPart</see> message<br/>
        /// or<br/>
        /// - it has a Content-Disposition header that says it is an attachment
        /// </summary>
        public bool IsAttachment
        {
            get
            {
                // Inline is the opposite of attachment
                return (!IsText && !IsMultiPart) || (ContentDisposition != null && !ContentDisposition.Inline);
            }
        }

        /// <summary>
        /// Returns the ContentDisposition's Name field, or FileName if not present
        /// </summary>
        public string Name { get { return _headers.Name ?? FileName; } }

        /// <summary>
        /// Returns the ContentDisposition's FileName field, or Name if not present
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// If this <see cref="MimeMessagePart"/> is a MultiPart message, then this property
        /// has a list of each of the Multiple parts that the message consists of.<br/>
        /// <br/>
        /// It is <see langword="null"/> if it is not a MultiPart message.<br/>
        /// Use <see cref="IsMultiPart"/> to check if this <see cref="MimeMessagePart"/> is a MultiPart message.
        /// </summary>
        public ICollection<MimeMessagePart> MessageParts { get { return new ReadOnlyCollection<MimeMessagePart>(_messageParts); } }
        #endregion

        #region Constructors
        /// <summary>
        /// Parses a MIME multi-part encoded content, using 7-bit encoding (www form using "multipart/form-data")
        /// </summary>
        internal MimeMessagePart(byte[] rawBody, NameValueCollection headers)
            : this(rawBody, new MessageHeader(headers))
        { }

        private MimeMessagePart(byte[] rawBody, MessageHeader headers)
        {
            if (rawBody == null)
                throw new ArgumentNullException("rawBody");

            if (headers == null)
                throw new ArgumentNullException("headers");

            _headers = headers;
            FileName = FindFileName(ContentType, ContentDisposition, "(no name)");
            BodyEncoding = ParseBodyEncoding(ContentType.CharSet);

            // Initialize the MessageParts property, with room to as many bodies as we have found
            _messageParts = new List<MimeMessagePart>();

            ParseBody(rawBody);
        }
        #endregion

        #region Parsing
        static Encoding ParseBodyEncoding(string characterSet)
        {
            // Default encoding in Mime messages is US-ASCII
            Encoding encoding = Encoding.ASCII;

            // If the character set was specified, find the encoding that the character
            // set describes, and use that one instead
            if (!string.IsNullOrEmpty(characterSet))
                encoding = EncodingFinder.FindEncoding(characterSet);

            return encoding;
        }

        private static string FindFileName(ContentType contentType, ContentDisposition contentDisposition, string defaultName)
        {
            if (contentType == null)
                throw new ArgumentNullException("contentType");

            if (contentDisposition != null && contentDisposition.FileName != null)
                return contentDisposition.FileName;

            if (contentType.Name != null)
                return contentType.Name;

            return defaultName;
        }

        private void ParseBody(byte[] rawBody)
        {
            if (IsMultiPart)
            {
                // Parses a MultiPart message
                ParseMultiPartBody(rawBody);
            }
            else
            {
                // Parses a non MultiPart message
                // Decode the body accodingly and set the Body property
                Body = DecodeBody(rawBody, ContentTransferEncoding);
            }
        }

        private void ParseMultiPartBody(byte[] rawBody)
        {
            // Fetch out the boundary used to delimit the messages within the body
            string multipartBoundary = ContentType.Boundary;

            // Fetch the individual MultiPart message parts using the MultiPart boundary
            List<byte[]> bodyParts = GetMultiPartParts(rawBody, multipartBoundary);

            // Now parse each byte array as a message body and add it the the MessageParts property
            foreach (byte[] bodyPart in bodyParts)
            {
                MimeMessagePart messagePart = GetMessagePart(bodyPart);
                _messageParts.Add(messagePart);
            }
        }

        private static MimeMessagePart GetMessagePart(byte[] rawMessageContent)
        {
            // Find the headers and the body parts of the byte array
            MessageHeader headers;
            byte[] body;
            HeaderExtractor.ExtractHeadersAndBody(rawMessageContent, out headers, out body);

            // Create a new MessagePart from the headers and the body
            return new MimeMessagePart(body, headers);
        }

        private static List<byte[]> GetMultiPartParts(byte[] rawBody, string multipPartBoundary)
        {
            // This is the list we want to return
            List<byte[]> messageBodies = new List<byte[]>();

            // Create a stream from which we can find MultiPart boundaries
            using (MemoryStream stream = new MemoryStream(rawBody))
            {
                bool lastMultipartBoundaryEncountered;

                // Find the start of the first message in this multipart
                // Since the method returns the first character on a the line containing the MultiPart boundary, we
                // need to add the MultiPart boundary with prepended "--" and appended CRLF pair to the position returned.
                int startLocation = FindPositionOfNextMultiPartBoundary(stream, multipPartBoundary, out lastMultipartBoundaryEncountered) + ("--" + multipPartBoundary + "\r\n").Length;
                while (true)
                {
                    // When we have just parsed the last multipart entry, stop parsing on
                    if (lastMultipartBoundaryEncountered)
                        break;

                    // Find the end location of the current multipart
                    // Since the method returns the first character on a the line containing the MultiPart boundary, we
                    // need to go a CRLF pair back, so that we do not get that into the body of the message part
                    int stopLocation = FindPositionOfNextMultiPartBoundary(stream, multipPartBoundary, out lastMultipartBoundaryEncountered) - "\r\n".Length;

                    // If we could not find the next multipart boundary, but we had not yet discovered the last boundary, then
                    // we will consider the rest of the bytes as contained in a last message part.
                    if (stopLocation <= -1)
                    {
                        // Include everything except the last CRLF.
                        stopLocation = (int)stream.Length - "\r\n".Length;

                        // We consider this as the last part
                        lastMultipartBoundaryEncountered = true;

                        // Special case: when the last multipart delimiter is not ending with "--", but is indeed the last
                        // one, then the next multipart would contain nothing, and we should not include such one.
                        if (startLocation >= stopLocation)
                            break;
                    }

                    // We have now found the start and end of a message part
                    // Now we create a byte array with the correct length and put the message part's bytes into
                    // it and add it to our list we want to return
                    int length = stopLocation - startLocation;
                    byte[] messageBody = new byte[length];
                    Array.Copy(rawBody, startLocation, messageBody, 0, length);
                    messageBodies.Add(messageBody);

                    // We want to advance to the next message parts start.
                    // We can find this by jumping forward the MultiPart boundary from the last
                    // message parts end position
                    startLocation = stopLocation + ("\r\n" + "--" + multipPartBoundary + "\r\n").Length;
                }
            }

            // We are done
            return messageBodies;
        }

        private static int FindPositionOfNextMultiPartBoundary(Stream stream, string multiPartBoundary, out bool lastMultipartBoundaryFound)
        {
            lastMultipartBoundaryFound = false;
            while (true)
            {
                // Get the current position. This is the first position on the line - no characters of the line will
                // have been read yet
                int currentPos = (int)stream.Position;

                // Read the line
                string line = ReadLineAsAscii(stream);

                // If we kept reading until there was no more lines, we did not meet
                // the MultiPart boundary. -1 is then returned to describe this.
                if (line == null)
                    return -1;

                // The MultiPart boundary is the MultiPartBoundary with "--" in front of it
                // which is to be at the very start of a line
                if (line.StartsWith("--" + multiPartBoundary, StringComparison.Ordinal))
                {
                    // Check if the found boundary was also the last one
                    lastMultipartBoundaryFound = line.StartsWith("--" + multiPartBoundary + "--", StringComparison.OrdinalIgnoreCase);
                    return currentPos;
                }
            }
        }

        private static byte[] DecodeBody(byte[] messageBody, ContentTransferEncoding contentTransferEncoding)
        {
            if (messageBody == null)
                throw new ArgumentNullException("messageBody");

            switch (contentTransferEncoding)
            {
                case ContentTransferEncoding.QuotedPrintable:
                    // If encoded in QuotedPrintable, everything in the body is in US-ASCII
                    return QuotedPrintable.DecodeContentTransferEncoding(Encoding.ASCII.GetString(messageBody));

                case ContentTransferEncoding.Base64:
                    // If encoded in Base64, everything in the body is in US-ASCII
                    return Convert.FromBase64String(Encoding.ASCII.GetString(messageBody));

                case ContentTransferEncoding.SevenBit:
                case ContentTransferEncoding.Binary:
                case ContentTransferEncoding.EightBit:
                    // We do not have to do anything
                    return messageBody;

                default:
                    throw new ArgumentOutOfRangeException("contentTransferEncoding");
            }
        }
        #endregion

        #region Public methods
        string GetBodyAsText()
        {
            return BodyEncoding.GetString(Body);
        }

        /// <summary>
        /// Gets this MessagePart's <see cref="Body"/> as text, or null if the IsMultiPart property is true.<br/>
        /// </summary>
        public string Text
        {
            get { return IsMultiPart ? null : GetBodyAsText(); }
        }

        #endregion
        #region ReadLineAsAscii
        static byte[] ReadLineAsBytes(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (MemoryStream memoryStream = new MemoryStream())
            {
                while (true)
                {
                    int justRead = stream.ReadByte();
                    if (justRead == -1 && memoryStream.Length > 0)
                        break;

                    // Check if we started at the end of the stream we read from
                    // and we have not read anything from it yet
                    if (justRead == -1 && memoryStream.Length == 0)
                        return null;

                    char readChar = (char)justRead;

                    // Do not write \r or \n
                    if (readChar != '\r' && readChar != '\n')
                        memoryStream.WriteByte((byte)justRead);

                    // Last point in CRLF pair
                    if (readChar == '\n')
                        break;
                }

                return memoryStream.ToArray();
            }
        }

        internal static string ReadLineAsAscii(Stream stream)
        {
            byte[] readFromStream = ReadLineAsBytes(stream);
            return readFromStream != null ? Encoding.ASCII.GetString(readFromStream) : null;
        }
        #endregion
        
        #region MessageHeader
        sealed class MessageHeader
        {
            public NameValueCollection Headers { get; private set; }
            public ContentTransferEncoding ContentTransferEncoding { get; private set; }
            public ContentType ContentType { get; private set; }
            public string Name { get; private set; }
            public ContentDisposition ContentDisposition { get; private set; }
            public string ContentId { get; private set; }
            public string ContentDescription { get; private set; }

            internal MessageHeader(NameValueCollection headers)
            {
                if (headers == null)
                    throw new ArgumentNullException("headers");

                Headers = headers;

                // 7BIT is the default ContentTransferEncoding (assumed if not set)
                ContentTransferEncoding = ContentTransferEncoding.SevenBit;

                // text/plain; charset=us-ascii is the default ContentType
                ContentType = new ContentType("text/plain; charset=us-ascii");

                // Now parse the actual headers
                ParseHeaders(headers);
            }

            private void ParseHeaders(NameValueCollection headers)
            {
                if (headers == null)
                    throw new ArgumentNullException("headers");

                // Now begin to parse the header values
                foreach (string headerName in headers.Keys)
                {
                    string[] headerValues = headers.GetValues(headerName);
                    if (headerValues != null)
                    {
                        foreach (string headerValue in headerValues)
                        {
                            ParseHeader(headerName, headerValue);
                        }
                    }
                }
            }

            private void ParseHeader(string headerName, string headerValue)
            {
                if (headerName == null)
                    throw new ArgumentNullException("headerName");

                if (headerValue == null)
                    throw new ArgumentNullException("headerValue");

                switch (headerName.ToUpperInvariant())
                {
                    // See http://tools.ietf.org/html/rfc2045#section-6
                    // See ContentTransferEncoding class for more details
                    case "CONTENT-TRANSFER-ENCODING":
                        ContentTransferEncoding = HeaderFieldParser.ParseContentTransferEncoding(headerValue.Trim());
                        break;

                    // See http://tools.ietf.org/html/rfc2045#section-5.1
                    // Example: Content-type: text/plain; charset="us-ascii"
                    case "CONTENT-TYPE":
                        ContentType = HeaderFieldParser.ParseContentType(headerValue);
                        break;

                    // See http://tools.ietf.org/html/rfc2183
                    case "CONTENT-DISPOSITION":
                        string name;
                        ContentDisposition = HeaderFieldParser.ParseContentDisposition(headerValue, out name);
                        Name = name ?? ContentDisposition.FileName;
                        break;

                    // See http://tools.ietf.org/html/rfc2045#section-8
                    case "CONTENT-DESCRIPTION":
                        // Human description of for example a file. Can be encoded
                        ContentDescription = EncodedWord.Decode(headerValue.Trim());
                        break;

                    // See http://tools.ietf.org/html/rfc2045#section-7
                    // Example: <foo4*foo1@bar.net>
                    case "CONTENT-ID":
                        ContentId = HeaderFieldParser.ParseId(headerValue);
                        break;
                }
            }
        }
        #endregion
        #region QuotedPrintable
        static class QuotedPrintable
        {
            public static string DecodeEncodedWord(string toDecode, Encoding encoding)
            {
                if (toDecode == null)
                    throw new ArgumentNullException("toDecode");

                if (encoding == null)
                    throw new ArgumentNullException("encoding");

                // Decode the QuotedPrintable string and return it
                return encoding.GetString(Rfc2047QuotedPrintableDecode(toDecode, true));
            }
            public static byte[] DecodeContentTransferEncoding(string toDecode)
            {
                if (toDecode == null)
                    throw new ArgumentNullException("toDecode");

                // Decode the QuotedPrintable string and return it
                return Rfc2047QuotedPrintableDecode(toDecode, false);
            }
            private static byte[] Rfc2047QuotedPrintableDecode(string toDecode, bool encodedWordVariant)
            {
                if (toDecode == null)
                    throw new ArgumentNullException("toDecode");

                // Create a byte array builder which is roughly equivalent to a StringBuilder
                using (MemoryStream byteArrayBuilder = new MemoryStream())
                {
                    // Remove illegal control characters
                    toDecode = RemoveIllegalControlCharacters(toDecode);

                    // Run through the whole string that needs to be decoded
                    for (int i = 0; i < toDecode.Length; i++)
                    {
                        char currentChar = toDecode[i];
                        if (currentChar == '=')
                        {
                            // Check that there is at least two characters behind the equal sign
                            if (toDecode.Length - i < 3)
                            {
                                // We are at the end of the toDecode string, but something is missing. Handle it the way RFC 2045 states
                                WriteAllBytesToStream(byteArrayBuilder, DecodeEqualSignNotLongEnough(toDecode.Substring(i)));

                                // Since it was the last part, we should stop parsing anymore
                                break;
                            }

                            // Decode the Quoted-Printable part
                            string quotedPrintablePart = toDecode.Substring(i, 3);
                            WriteAllBytesToStream(byteArrayBuilder, DecodeEqualSign(quotedPrintablePart));

                            // We now consumed two extra characters. Go forward two extra characters
                            i += 2;
                        }
                        else
                        {
                            // This character is not quoted printable hex encoded.

                            // Could it be the _ character, which represents space
                            // and are we using the encoded word variant of QuotedPrintable
                            if (currentChar == '_' && encodedWordVariant)
                            {
                                // The RFC specifies that the "_" always represents hexadecimal 20 even if the
                                // SPACE character occupies a different code position in the character set in use.
                                byteArrayBuilder.WriteByte(0x20);
                            }
                            else
                            {
                                // This is not encoded at all. This is a literal which should just be included into the output.
                                byteArrayBuilder.WriteByte((byte)currentChar);
                            }
                        }
                    }

                    return byteArrayBuilder.ToArray();
                }
            }
            private static void WriteAllBytesToStream(Stream stream, byte[] toWrite)
            {
                stream.Write(toWrite, 0, toWrite.Length);
            }
            private static string RemoveIllegalControlCharacters(string input)
            {
                if (input == null)
                    throw new ArgumentNullException("input");

                // First we remove any \r or \n which is not part of a \r\n pair
                input = RemoveCarriageReturnAndNewLinewIfNotInPair(input);

                // Here only legal \r\n is left over
                // We now simply keep them, and the \t which is also allowed
                // \x0A = \n
                // \x0D = \r
                // \x09 = \t)
                return Regex.Replace(input, "[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
            }
            private static string RemoveCarriageReturnAndNewLinewIfNotInPair(string input)
            {
                if (input == null)
                    throw new ArgumentNullException("input");

                // Use this for building up the new string. This is used for performance instead
                // of altering the input string each time a illegal token is found
                StringBuilder newString = new StringBuilder(input.Length);

                for (int i = 0; i < input.Length; i++)
                {
                    // There is a character after it
                    // Check for lonely \r
                    // There is a lonely \r if it is the last character in the input or if there
                    // is no \n following it
                    if (input[i] == '\r' && (i + 1 >= input.Length || input[i + 1] != '\n'))
                    {
                        // Illegal token \r found. Do not add it to the new string

                        // Check for lonely \n
                        // There is a lonely \n if \n is the first character or if there
                        // is no \r in front of it
                    }
                    else if (input[i] == '\n' && (i - 1 < 0 || input[i - 1] != '\r'))
                    {
                        // Illegal token \n found. Do not add it to the new string
                    }
                    else
                    {
                        // No illegal tokens found. Simply insert the character we are at
                        // in our new string
                        newString.Append(input[i]);
                    }
                }

                return newString.ToString();
            }

            private static byte[] DecodeEqualSignNotLongEnough(string decode)
            {
                if (decode == null)
                    throw new ArgumentNullException("decode");

                // We can only decode wrong length equal signs
                if (decode.Length >= 3)
                    throw new ArgumentException("decode must have length lower than 3", "decode");

                // First char must be =
                if (decode[0] != '=')
                    throw new ArgumentException("First part of decode must be an equal sign", "decode");

                // We will now believe that the string sent to us, was actually not encoded
                // Therefore it must be in US-ASCII and we will return the bytes it corrosponds to
                return Encoding.ASCII.GetBytes(decode);
            }

            private static byte[] DecodeEqualSign(string decode)
            {
                if (decode == null)
                    throw new ArgumentNullException("decode");

                // We can only decode the string if it has length 3 - other calls to this function is invalid
                if (decode.Length != 3)
                    throw new ArgumentException("decode must have length 3", "decode");

                // First char must be =
                if (decode[0] != '=')
                    throw new ArgumentException("decode must start with an equal sign", "decode");

                // There are two cases where an equal sign might appear
                // It might be a
                //   - hex-string like =3D, denoting the character with hex value 3D
                //   - it might be the last character on the line before a CRLF
                //     pair, denoting a soft linebreak, which simply
                //     splits the text up, because of the 76 chars per line restriction
                if (decode.Contains("\r\n"))
                {
                    // Soft break detected
                    // We want to return string.Empty which is equivalent to a zero-length byte array
                    return new byte[0];
                }

                // Hex string detected. Convertion needed.
                // It might be that the string located after the equal sign is not hex characters
                // An example: =JU
                // In that case we would like to catch the FormatException and do something else
                try
                {
                    // The number part of the string is the last two digits. Here we simply remove the equal sign
                    string numberString = decode.Substring(1);

                    // Now we create a byte array with the converted number encoded in the string as a hex value (base 16)
                    // This will also handle illegal encodings like =3d where the hex digits are not uppercase,
                    // which is a robustness requirement from RFC 2045.
                    byte[] oneByte = new[] { Convert.ToByte(numberString, 16) };

                    // Simply return our one byte byte array
                    return oneByte;
                }
                catch (FormatException)
                {
                    // RFC 2045 says about robust implementation:
                    // An "=" followed by a character that is neither a
                    // hexadecimal digit (including "abcdef") nor the CR
                    // character of a CRLF pair is illegal.  This case can be
                    // the result of US-ASCII text having been included in a
                    // quoted-printable part of a message without itself
                    // having been subjected to quoted-printable encoding.  A
                    // reasonable approach by a robust implementation might be
                    // to include the "=" character and the following
                    // character in the decoded data without any
                    // transformation and, if possible, indicate to the user
                    // that proper decoding was not possible at this point in
                    // the data.

                    // So we choose to believe this is actually an un-encoded string
                    // Therefore it must be in US-ASCII and we will return the bytes it corrosponds to
                    return Encoding.ASCII.GetBytes(decode);
                }
            }
        }
        #endregion
        #region HeaderFieldParser
        static class HeaderFieldParser
        {
            public static ContentTransferEncoding ParseContentTransferEncoding(string headerValue)
            {
                if (headerValue == null)
                    throw new ArgumentNullException("headerValue");

                switch (headerValue.Trim().ToUpperInvariant())
                {
                    case "7BIT":
                        return ContentTransferEncoding.SevenBit;

                    case "8BIT":
                        return ContentTransferEncoding.EightBit;

                    case "QUOTED-PRINTABLE":
                        return ContentTransferEncoding.QuotedPrintable;

                    case "BASE64":
                        return ContentTransferEncoding.Base64;

                    case "BINARY":
                        return ContentTransferEncoding.Binary;

                    // If a wrong argument is passed to this parser method, then we assume
                    // default encoding, which is SevenBit.
                    // This is to ensure that we do not throw exceptions, even if the email not MIME valid.
                    default:
                        //DefaultLogger.Log.LogDebug("Wrong ContentTransferEncoding was used. It was: " + headerValue);
                        return ContentTransferEncoding.SevenBit;
                }
            }
            public static MailPriority ParseImportance(string headerValue)
            {
                if (headerValue == null)
                    throw new ArgumentNullException("headerValue");

                switch (headerValue.ToUpperInvariant())
                {
                    case "5":
                    case "HIGH":
                        return MailPriority.High;

                    case "3":
                    case "NORMAL":
                        return MailPriority.Normal;

                    case "1":
                    case "LOW":
                        return MailPriority.Low;

                    default:
                        //DefaultLogger.Log.LogDebug("HeaderFieldParser: Unknown importance value: \"" + headerValue + "\". Using default of normal importance.");
                        return MailPriority.Normal;
                }
            }
            public static ContentType ParseContentType(string headerValue)
            {
                if (headerValue == null)
                    throw new ArgumentNullException("headerValue");

                // We create an empty Content-Type which we will fill in when we see the values
                ContentType contentType = new ContentType();

                // Now decode the parameters
                List<KeyValuePair<string, string>> parameters = Rfc2231Decoder.Decode(headerValue);

                foreach (KeyValuePair<string, string> keyValuePair in parameters)
                {
                    string key = keyValuePair.Key.ToUpperInvariant().Trim();
                    string value = RemoveQuotesIfAny(keyValuePair.Value.Trim());
                    switch (key)
                    {
                        case "":
                            // This is the MediaType - it has no key since it is the first one mentioned in the
                            // headerValue and has no = in it.

                            // Check for illegal content-type
                            if (value.ToUpperInvariant().Equals("TEXT"))
                                value = "text/plain";

                            contentType.MediaType = value;
                            break;

                        case "BOUNDARY":
                            contentType.Boundary = value;
                            break;

                        case "CHARSET":
                            contentType.CharSet = value;
                            break;

                        case "NAME":
                            contentType.Name = EncodedWord.Decode(value);
                            break;

                        default:
                            // This is to shut up the code help that is saying that contentType.Parameters
                            // can be null - which it cant!
                            if (contentType.Parameters == null)
                                throw new Exception("The ContentType parameters property is null. This will never be thrown.");

                            // We add the unknown value to our parameters list
                            // "Known" unknown values are:
                            // - title
                            // - report-type
                            contentType.Parameters.Add(key, value);
                            break;
                    }
                }

                return contentType;
            }
            public static ContentDisposition ParseContentDisposition(string headerValue, out string name)
            {
                name = null;
                if (headerValue == null)
                    throw new ArgumentNullException("headerValue");

                // See http://www.ietf.org/rfc/rfc2183.txt for RFC definition

                // Create empty ContentDisposition - we will fill in details as we read them
                ContentDisposition contentDisposition = new ContentDisposition();

                // Now decode the parameters
                List<KeyValuePair<string, string>> parameters = Rfc2231Decoder.Decode(headerValue);

                foreach (KeyValuePair<string, string> keyValuePair in parameters)
                {
                    string key = keyValuePair.Key.ToUpperInvariant().Trim();
                    string value = keyValuePair.Value;
                    switch (key)
                    {
                        case "":
                            // This is the DispisitionType - it has no key since it is the first one
                            // and has no = in it.
                            contentDisposition.DispositionType = value;
                            break;

                        // The correct name of the parameter is filename, but some emails also contains the parameter
                        // name, which also holds the name of the file. Therefore we use both names for the same field.
                        case "NAME":
                            contentDisposition.FileName = name = EncodedWord.Decode(RemoveQuotesIfAny(value));
                            break;
                        case "FILENAME":
                            // The filename might be in qoutes, and it might be encoded-word encoded
                            contentDisposition.FileName = EncodedWord.Decode(RemoveQuotesIfAny(value));
                            break;

                        case "CREATION-DATE":
                            // Notice that we need to create a new DateTime because of a failure in .NET 2.0.
                            // The failure is: you cannot give contentDisposition a DateTime with a Kind of UTC
                            // It will set the CreationDate correctly, but when trying to read it out it will throw an exception.
                            // It is the same with ModificationDate and ReadDate.
                            // This is fixed in 4.0 - maybe in 3.0 too.
                            // Therefore we create a new DateTime which have a DateTimeKind set to unspecified
                            DateTime creationDate = new DateTime(Rfc2822DateTime.StringToDate(RemoveQuotesIfAny(value)).Ticks);
                            contentDisposition.CreationDate = creationDate;
                            break;

                        case "MODIFICATION-DATE":
                            DateTime midificationDate = new DateTime(Rfc2822DateTime.StringToDate(RemoveQuotesIfAny(value)).Ticks);
                            contentDisposition.ModificationDate = midificationDate;
                            break;

                        case "READ-DATE":
                            DateTime readDate = new DateTime(Rfc2822DateTime.StringToDate(RemoveQuotesIfAny(value)).Ticks);
                            contentDisposition.ReadDate = readDate;
                            break;

                        case "SIZE":
                            contentDisposition.Size = int.Parse(RemoveQuotesIfAny(value), CultureInfo.InvariantCulture);
                            break;

                        default:
                            if (key.StartsWith("X-"))
                            {
                                contentDisposition.Parameters.Add(key, RemoveQuotesIfAny(value));
                                break;
                            }

                            throw new ArgumentException("Unknown parameter in Content-Disposition. Ask developer to fix! Parameter: " + key);
                    }
                }

                return contentDisposition;
            }

            internal static string ParseId(string headerValue)
            {
                // Remove whitespace in front and behind since
                // whitespace is allowed there
                // Remove the last > and the first <
                return headerValue.Trim().TrimEnd('>').TrimStart('<');
            }

            internal static List<string> ParseMultipleIDs(string headerValue)
            {
                List<string> returner = new List<string>();

                // Split the string by >
                // We cannot use ' ' (space) here since this is a possible value:
                // <test@test.com><test2@test.com>
                string[] ids = headerValue.Trim().Split(new[] { '>' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string id in ids)
                {
                    returner.Add(ParseId(id));
                }

                return returner;
            }

            internal static string RemoveQuotesIfAny(string text)
            {
                if (text == null)
                    throw new ArgumentNullException("text");

                // Check if there are qoutes at both ends
                if (text[0] == '"' && text[text.Length - 1] == '"')
                {
                    // Remove quotes at both ends
                    return text.Substring(1, text.Length - 2);
                }

                // If no quotes were found, the text is just returned
                return text;
            }
        }
        #endregion
        #region Rfc2231Decoder
        static class Rfc2231Decoder
        {
            internal static List<KeyValuePair<string, string>> Decode(string toDecode)
            {
                if (toDecode == null)
                    throw new ArgumentNullException("toDecode");

                // Normalize the input to take account for missing semicolons after parameters.
                // Example
                // text/plain; charset=\"iso-8859-1\" name=\"somefile.txt\" or
                // text/plain;\tcharset=\"iso-8859-1\"\tname=\"somefile.txt\"
                // is normalized to
                // text/plain; charset=\"iso-8859-1\"; name=\"somefile.txt\"
                // Only works for parameters inside quotes
                // \s = matches whitespace
                toDecode = Regex.Replace(toDecode, "=\\s*\"(?<value>[^\"]*)\"\\s", "=\"${value}\"; ");

                // Normalize 
                // Since the above only works for parameters inside quotes, we need to normalize
                // the special case with the first parameter.
                // Example:
                // attachment filename="foo"
                // is normalized to
                // attachment; filename="foo"
                // ^ = matches start of line (when not inside square bracets [])
                toDecode = Regex.Replace(toDecode, @"^(?<first>[^;\s]+)\s(?<second>[^;\s]+)", "${first}; ${second}");

                // Split by semicolon, but only if not inside quotes
                List<string> splitted = SplitStringWithCharNotInsideQuotes(toDecode.Trim(), ';');

                List<KeyValuePair<string, string>> collection = new List<KeyValuePair<string, string>>(splitted.Count);

                foreach (string part in splitted)
                {
                    // Empty strings should not be processed
                    if (part.Trim().Length == 0)
                        continue;

                    string[] keyValue = part.Trim().Split(new[] { '=' }, 2);
                    if (keyValue.Length == 1)
                    {
                        collection.Add(new KeyValuePair<string, string>("", keyValue[0]));
                    }
                    else if (keyValue.Length == 2)
                    {
                        collection.Add(new KeyValuePair<string, string>(keyValue[0], keyValue[1]));
                    }
                    else
                    {
                        throw new ArgumentException("When splitting the part \"" + part + "\" by = there was " + keyValue.Length + " parts. Only 1 and 2 are supported");
                    }
                }

                return DecodePairs(collection);
            }

            internal static List<string> SplitStringWithCharNotInsideQuotes(string input, char toSplitAt)
            {
                List<string> elements = new List<string>();

                int lastSplitLocation = 0;
                bool insideQuote = false;

                char[] characters = input.ToCharArray();

                for (int i = 0; i < characters.Length; i++)
                {
                    char character = characters[i];
                    if (character == '\"')
                        insideQuote = !insideQuote;

                    // Only split if we are not inside quotes
                    if (character == toSplitAt && !insideQuote)
                    {
                        // We need to split
                        int length = i - lastSplitLocation;
                        elements.Add(input.Substring(lastSplitLocation, length));

                        // Update last split location
                        // + 1 so that we do not include the character used to split with next time
                        lastSplitLocation = i + 1;
                    }
                }

                // Add the last part
                elements.Add(input.Substring(lastSplitLocation, input.Length - lastSplitLocation));

                return elements;
            }

            internal static List<KeyValuePair<string, string>> DecodePairs(List<KeyValuePair<string, string>> pairs)
            {
                if (pairs == null)
                    throw new ArgumentNullException("pairs");

                List<KeyValuePair<string, string>> resultPairs = new List<KeyValuePair<string, string>>(pairs.Count);

                int pairsCount = pairs.Count;
                for (int i = 0; i < pairsCount; i++)
                {
                    KeyValuePair<string, string> currentPair = pairs[i];
                    string key = currentPair.Key;
                    string value = HeaderFieldParser.RemoveQuotesIfAny(currentPair.Value);

                    // Is it a continuation parameter? (encoded or not)
                    if (key.EndsWith("*0", StringComparison.OrdinalIgnoreCase) || key.EndsWith("*0*", StringComparison.OrdinalIgnoreCase))
                    {
                        // This encoding will not be used if we get into the if which tells us
                        // that the whole continuation is not encoded

                        string encoding = "notEncoded - Value here is never used";

                        // Now lets find out if it is encoded too.
                        if (key.EndsWith("*0*", StringComparison.OrdinalIgnoreCase))
                        {
                            // It is encoded.

                            // Fetch out the encoding for later use and decode the value
                            // If the value was not encoded as the email specified
                            // encoding will be set to null. This will be used later.
                            value = DecodeSingleValue(value, out encoding);

                            // Find the right key to use to store the full value
                            // Remove the start *0 which tells is it is a continuation, and the first one
                            // And remove the * afterwards which tells us it is encoded
                            key = key.Replace("*0*", "");
                        }
                        else
                        {
                            // It is not encoded, and no parts of the continuation is encoded either

                            // Find the right key to use to store the full value
                            // Remove the start *0 which tells is it is a continuation, and the first one
                            key = key.Replace("*0", "");
                        }

                        // The StringBuilder will hold the full decoded value from all continuation parts
                        StringBuilder builder = new StringBuilder();

                        // Append the decoded value
                        builder.Append(value);

                        // Now go trough the next keys to see if they are part of the continuation
                        for (int j = i + 1, continuationCount = 1; j < pairsCount; j++, continuationCount++)
                        {
                            string jKey = pairs[j].Key;
                            string valueJKey = HeaderFieldParser.RemoveQuotesIfAny(pairs[j].Value);

                            if (jKey.Equals(key + "*" + continuationCount))
                            {
                                // This value part of the continuation is not encoded
                                // Therefore remove qoutes if any and add to our stringbuilder
                                builder.Append(valueJKey);

                                // Remember to increment i, as we have now treated one more KeyValuePair
                                i++;
                            }
                            else if (jKey.Equals(key + "*" + continuationCount + "*"))
                            {
                                // We will not get into this part if the first part was not encoded
                                // Therefore the encoding will only be used if and only if the
                                // first part was encoded, in which case we have remembered the encoding used

                                // Sometimes an email creator says that a string was encoded, but it really
                                // `was not. This is to catch that problem.
                                if (encoding != null)
                                {
                                    // This value part of the continuation is encoded
                                    // the encoding is not given in the current value,
                                    // but was given in the first continuation, which we remembered for use here
                                    valueJKey = DecodeSingleValue(valueJKey, encoding);
                                }
                                builder.Append(valueJKey);

                                // Remember to increment i, as we have now treated one more KeyValuePair
                                i++;
                            }
                            else
                            {
                                // No more keys for this continuation
                                break;
                            }
                        }

                        // Add the key and the full value as a pair
                        value = builder.ToString();
                        resultPairs.Add(new KeyValuePair<string, string>(key, value));
                    }
                    else if (key.EndsWith("*", StringComparison.OrdinalIgnoreCase))
                    {
                        // This parameter is only encoded - it is not part of a continuation
                        // We need to change the key from "<key>*" to "<key>" and decode the value

                        // To get the key we want, we remove the last * that denotes
                        // that the value hold by the key was encoded
                        key = key.Replace("*", "");

                        // Decode the value
                        string throwAway;
                        value = DecodeSingleValue(value, out throwAway);

                        // Now input the new value with the new key
                        resultPairs.Add(new KeyValuePair<string, string>(key, value));
                    }
                    else
                    {
                        // Fully normal key - the value is not encoded
                        // Therefore nothing to do, and we can simply pass the pair
                        // as being decoded now
                        resultPairs.Add(currentPair);
                    }
                }

                return resultPairs;
            }

            private static string DecodeSingleValue(string toDecode, out string encodingUsed)
            {
                if (toDecode == null)
                    throw new ArgumentNullException("toDecode");

                // Check if input has a part describing the encoding
                if (toDecode.IndexOf('\'') == -1)
                {
                    // The input was not encoded (at least not valid) and it is returned as is
                    //DefaultLogger.Log.LogDebug("Rfc2231Decoder: Someone asked me to decode a string which was not encoded - returning raw string. Input: " + toDecode);
                    encodingUsed = null;
                    return toDecode;
                }
                encodingUsed = toDecode.Substring(0, toDecode.IndexOf('\''));
                toDecode = toDecode.Substring(toDecode.LastIndexOf('\'') + 1);
                return DecodeSingleValue(toDecode, encodingUsed);
            }

            private static string DecodeSingleValue(string valueToDecode, string encoding)
            {
                if (valueToDecode == null)
                    throw new ArgumentNullException("valueToDecode");

                if (encoding == null)
                    throw new ArgumentNullException("encoding");

                // The encoding used is the same as QuotedPrintable, we only
                // need to change % to =
                // And otherwise make it look like the correct EncodedWord encoding
                valueToDecode = "=?" + encoding + "?Q?" + valueToDecode.Replace("%", "=") + "?=";
                return EncodedWord.Decode(valueToDecode);
            }
        }
        #endregion
        #region Rfc2822DateTime
        static class Rfc2822DateTime
        {
            public static DateTime StringToDate(string inputDate)
            {
                if (inputDate == null)
                    throw new ArgumentNullException("inputDate");

                // Old date specification allows comments and a lot of whitespace
                inputDate = StripCommentsAndExcessWhitespace(inputDate);

                try
                {
                    // Extract the DateTime
                    DateTime dateTime = ExtractDateTime(inputDate);

                    // If a day-name is specified in the inputDate string, check if it fits with the date
                    ValidateDayNameIfAny(dateTime, inputDate);

                    // Convert the date into UTC
                    dateTime = new DateTime(dateTime.Ticks, DateTimeKind.Utc);

                    // Adjust according to the time zone
                    dateTime = AdjustTimezone(dateTime, inputDate);

                    // Return the parsed date
                    return dateTime;
                }
                catch (FormatException e)	// Convert.ToDateTime() Failure
                {
                    throw new ArgumentException("Could not parse date: " + e.Message + ". Input was: \"" + inputDate + "\"", e);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException("Could not parse date: " + e.Message + ". Input was: \"" + inputDate + "\"", e);
                }
            }

            private static DateTime AdjustTimezone(DateTime dateTime, string dateInput)
            {
                // We know that the timezones are always in the last part of the date input
                string[] parts = dateInput.Split(' ');
                string lastPart = parts[parts.Length - 1];

                // Convert timezones in older formats to [+-]dddd format.
                lastPart = Regex.Replace(lastPart, @"UT|GMT|EST|EDT|CST|CDT|MST|MDT|PST|PDT|[A-I]|[K-Y]|Z", MatchEvaluator);

                // Find the timezone specification
                // Example: Fri, 21 Nov 1997 09:55:06 -0600
                // finds -0600
                Match match = Regex.Match(lastPart, @"[\+-](?<hours>\d\d)(?<minutes>\d\d)");
                if (match.Success)
                {
                    // We have found that the timezone is in +dddd or -dddd format
                    // Add the number of hours and minutes to our found date
                    int hours = int.Parse(match.Groups["hours"].Value);
                    int minutes = int.Parse(match.Groups["minutes"].Value);

                    int factor = match.Value[0] == '+' ? -1 : 1;

                    dateTime = dateTime.AddHours(factor * hours);
                    dateTime = dateTime.AddMinutes(factor * minutes);

                    return dateTime;
                }

                // A timezone of -0000 is the same as doing nothing
                return dateTime;
            }

            private static string MatchEvaluator(Match match)
            {
                if (!match.Success)
                {
                    throw new ArgumentException("Match success are always true");
                }

                switch (match.Value)
                {
                    // "A" through "I"
                    // are equivalent to "+0100" through "+0900" respectively
                    case "A": return "+0100";
                    case "B": return "+0200";
                    case "C": return "+0300";
                    case "D": return "+0400";
                    case "E": return "+0500";
                    case "F": return "+0600";
                    case "G": return "+0700";
                    case "H": return "+0800";
                    case "I": return "+0900";

                    // "K", "L", and "M"
                    // are equivalent to "+1000", "+1100", and "+1200" respectively
                    case "K": return "+1000";
                    case "L": return "+1100";
                    case "M": return "+1200";

                    // "N" through "Y"
                    // are equivalent to "-0100" through "-1200" respectively
                    case "N": return "-0100";
                    case "O": return "-0200";
                    case "P": return "-0300";
                    case "Q": return "-0400";
                    case "R": return "-0500";
                    case "S": return "-0600";
                    case "T": return "-0700";
                    case "U": return "-0800";
                    case "V": return "-0900";
                    case "W": return "-1000";
                    case "X": return "-1100";
                    case "Y": return "-1200";

                    // "Z", "UT" and "GMT"
                    // is equivalent to "+0000"
                    case "Z":
                    case "UT":
                    case "GMT":
                        return "+0000";

                    // US time zones
                    case "EDT": return "-0400"; // EDT is semantically equivalent to -0400
                    case "EST": return "-0500"; // EST is semantically equivalent to -0500
                    case "CDT": return "-0500"; // CDT is semantically equivalent to -0500
                    case "CST": return "-0600"; // CST is semantically equivalent to -0600
                    case "MDT": return "-0600"; // MDT is semantically equivalent to -0600
                    case "MST": return "-0700"; // MST is semantically equivalent to -0700
                    case "PDT": return "-0700"; // PDT is semantically equivalent to -0700
                    case "PST": return "-0800"; // PST is semantically equivalent to -0800

                    default:
                        throw new ArgumentException("Unexpected input");
                }
            }

            private static DateTime ExtractDateTime(string dateInput)
            {
                // Matches the date and time part of a string
                // Example: Fri, 21 Nov 1997 09:55:06 -0600
                // Finds: 21 Nov 1997 09:55:06
                // Seconds does not need to be specified
                // Even though it is illigal, sometimes hours, minutes or seconds are only specified with one digit
                Match match = Regex.Match(dateInput, @"\d\d? .+ (\d\d\d\d|\d\d) \d?\d:\d?\d(:\d?\d)?");
                if (match.Success)
                {
                    return Convert.ToDateTime(match.Value, CultureInfo.InvariantCulture);
                }

                throw new InvalidDataException("The given date does not appear to be in a valid format: " + dateInput);
                //return DateTime.MinValue;
            }

            private static void ValidateDayNameIfAny(DateTime dateTime, string dateInput)
            {
                // Check if there is a day name in front of the date
                // Example: Fri, 21 Nov 1997 09:55:06 -0600
                if (dateInput.Length >= 4 && dateInput[3] == ',')
                {
                    string dayName = dateInput.Substring(0, 3);

                    // If a dayName was specified. Check that the dateTime and the dayName
                    // agrees on which day it is
                    // This is just a failure-check and could be left out
                    if ((dateTime.DayOfWeek == DayOfWeek.Monday && !dayName.Equals("Mon")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Tuesday && !dayName.Equals("Tue")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Wednesday && !dayName.Equals("Wed")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Thursday && !dayName.Equals("Thu")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Friday && !dayName.Equals("Fri")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Saturday && !dayName.Equals("Sat")) ||
                        (dateTime.DayOfWeek == DayOfWeek.Sunday && !dayName.Equals("Sun")))
                    {
                        throw new InvalidDataException("Day-name does not correspond to the weekday of the date: " + dateInput);
                    }
                }

                // If no day name was found no checks can be made
            }

            private static string StripCommentsAndExcessWhitespace(string input)
            {
                // Strip out comments
                // Also strips out nested comments
                input = Regex.Replace(input, @"(\((?>\((?<C>)|\)(?<-C>)|.?)*(?(C)(?!))\))", "");

                // Reduce any whitespace character to one space only
                input = Regex.Replace(input, @"\s+", " ");

                // Remove all initial whitespace
                input = Regex.Replace(input, @"^\s+", "");

                // Remove all ending whitespace
                input = Regex.Replace(input, @"\s+$", "");

                // Remove spaces at colons
                // Example: 22: 33 : 44 => 22:33:44
                input = Regex.Replace(input, @" ?: ?", ":");

                return input;
            }
        }
        #endregion
        #region EncodedWord
        static class EncodedWord
        {
            public static string Decode(string encodedWords)
            {
                if (encodedWords == null)
                    throw new ArgumentNullException("encodedWords");

                // Notice that RFC2231 redefines the BNF to
                // encoded-word := "=?" charset ["*" language] "?" encoded-text "?="
                // but no usage of this BNF have been spotted yet. It is here to
                // ease debugging if such a case is discovered.

                // This is the regex that should fit the BNF
                // RFC Says that NO WHITESPACE is allowed in this encoding, but there are examples
                // where whitespace is there, and therefore this regex allows for such.
                const string encodedWordRegex = @"\=\?(?<Charset>\S+?)\?(?<Encoding>\w)\?(?<Content>.+?)\?\=";
                // \w	Matches any word character including underscore. Equivalent to "[A-Za-z0-9_]".
                // \S	Matches any nonwhite space character. Equivalent to "[^ \f\n\r\t\v]".
                // +?   non-gready equivalent to +
                // (?<NAME>REGEX) is a named group with name NAME and regular expression REGEX

                // Any amount of linear-space-white between 'encoded-word's,
                // even if it includes a CRLF followed by one or more SPACEs,
                // is ignored for the purposes of display.
                // http://tools.ietf.org/html/rfc2047#page-12
                // Define a regular expression that captures two encoded words with some whitespace between them
                const string replaceRegex = @"(?<first>" + encodedWordRegex + @")\s+(?<second>" + encodedWordRegex + ")";

                // Then, find an occourance of such an expression, but remove the whitespace inbetween when found
                encodedWords = Regex.Replace(encodedWords, replaceRegex, "${first}${second}");

                string decodedWords = encodedWords;

                MatchCollection matches = Regex.Matches(encodedWords, encodedWordRegex);
                foreach (Match match in matches)
                {
                    // If this match was not a success, we should not use it
                    if (!match.Success) continue;

                    string fullMatchValue = match.Value;

                    string encodedText = match.Groups["Content"].Value;
                    string encoding = match.Groups["Encoding"].Value;
                    string charset = match.Groups["Charset"].Value;

                    // Get the encoding which corrosponds to the character set
                    Encoding charsetEncoding = EncodingFinder.FindEncoding(charset);

                    // Store decoded text here when done
                    string decodedText;

                    // Encoding may also be written in lowercase
                    switch (encoding.ToUpperInvariant())
                    {
                        // RFC:
                        // The "B" encoding is identical to the "BASE64" 
                        // encoding defined by RFC 2045.
                        // http://tools.ietf.org/html/rfc2045#section-6.8
                        case "B":
                            decodedText = charsetEncoding.GetString(Convert.FromBase64String(encodedText));
                            break;

                        // RFC:
                        // The "Q" encoding is similar to the "Quoted-Printable" content-
                        // transfer-encoding defined in RFC 2045.
                        // There are more details to this. Please check
                        // http://tools.ietf.org/html/rfc2047#section-4.2
                        // 
                        case "Q":
                            decodedText = QuotedPrintable.DecodeEncodedWord(encodedText, charsetEncoding);
                            break;

                        default:
                            throw new ArgumentException("The encoding " + encoding + " was not recognized");
                    }

                    // Repalce our encoded value with our decoded value
                    decodedWords = decodedWords.Replace(fullMatchValue, decodedText);
                }

                return decodedWords;
            }
        }
        #endregion
        #region EncodingFinder
        static class EncodingFinder
        {
            public delegate Encoding FallbackDecoderDelegate(string characterSet);
            public static FallbackDecoderDelegate FallbackDecoder { private get; set; }
            private static Dictionary<string, Encoding> EncodingMap { get; set; }

            static EncodingFinder()
            {
                Reset();
            }

            internal static void Reset()
            {
                EncodingMap = new Dictionary<string, Encoding>();
                FallbackDecoder = null;

                // Some emails incorrectly specify the encoding as utf8, but it should have been utf-8.
                AddMapping("utf8", Encoding.UTF8);
            }

            internal static Encoding FindEncoding(string characterSet)
            {
                if (characterSet == null)
                    throw new ArgumentNullException("characterSet");

                string charSetUpper = characterSet.ToUpperInvariant();

                // Check if the characterSet is explicitly mapped to an encoding
                if (EncodingMap.ContainsKey(charSetUpper))
                    return EncodingMap[charSetUpper];

                // Try to find the generally find the encoding
                try
                {
                    if (charSetUpper.Contains("WINDOWS") || charSetUpper.Contains("CP"))
                    {
                        // It seems the characterSet contains an codepage value, which we should use to parse the encoding
                        charSetUpper = charSetUpper.Replace("CP", ""); // Remove cp
                        charSetUpper = charSetUpper.Replace("WINDOWS", ""); // Remove windows
                        charSetUpper = charSetUpper.Replace("-", ""); // Remove - which could be used as cp-1554

                        // Now we hope the only thing left in the characterSet is numbers.
                        int codepageNumber = int.Parse(charSetUpper, CultureInfo.InvariantCulture);

                        return Encoding.GetEncoding(codepageNumber);
                    }

                    // It seems there is no codepage value in the characterSet. It must be a named encoding
                    return Encoding.GetEncoding(characterSet);
                }
                catch (ArgumentException)
                {
                    // The encoding could not be found generally. 
                    // Try to use the FallbackDecoder if it is defined.

                    // Check if it is defined
                    if (FallbackDecoder == null)
                        throw; // It was not defined - throw catched exception

                    // Use the FallbackDecoder
                    Encoding fallbackDecoderResult = FallbackDecoder(characterSet);

                    // Check if the FallbackDecoder had a solution
                    if (fallbackDecoderResult != null)
                        return fallbackDecoderResult;

                    // If no solution was found, throw catched exception
                    throw;
                }
            }

            public static void AddMapping(string characterSet, Encoding encoding)
            {
                if (characterSet == null)
                    throw new ArgumentNullException("characterSet");

                if (encoding == null)
                    throw new ArgumentNullException("encoding");

                // Add the mapping using uppercase
                EncodingMap.Add(characterSet.ToUpperInvariant(), encoding);
            }
        }
        #endregion
        #region HeaderExtractor
        static class HeaderExtractor
        {
            private static int FindHeaderEndPosition(byte[] messageContent)
            {
                // Convert the byte array into a stream
                using (Stream stream = new MemoryStream(messageContent))
                {
                    while (true)
                    {
                        // Read a line from the stream. We know headers are in US-ASCII
                        // therefore it is not problem to read them as such
                        string line = MimeMessagePart.ReadLineAsAscii(stream);

                        // The end of headers is signaled when a blank line is found
                        // or if the line is null - in which case the email is actually an email with
                        // only headers but no body
                        if (string.IsNullOrEmpty(line))
                            return (int)stream.Position;
                    }
                }
            }
            public static void ExtractHeadersAndBody(byte[] fullRawMessage, out MessageHeader headers, out byte[] body)
            {
                if (fullRawMessage == null)
                    throw new ArgumentNullException("fullRawMessage");

                // Find the end location of the headers
                int endOfHeaderLocation = FindHeaderEndPosition(fullRawMessage);

                // The headers are always in ASCII - therefore we can convert the header part into a string
                // using US-ASCII encoding
                string headersString = Encoding.ASCII.GetString(fullRawMessage, 0, endOfHeaderLocation);

                // Now parse the headers to a NameValueCollection
                NameValueCollection headersUnparsedCollection = ExtractHeaders(headersString);

                // Use the NameValueCollection to parse it into a strongly-typed MessageHeader header
                headers = new MessageHeader(headersUnparsedCollection);

                // Since we know where the headers end, we also know where the body is
                // Copy the body part into the body parameter
                body = new byte[fullRawMessage.Length - endOfHeaderLocation];
                Array.Copy(fullRawMessage, endOfHeaderLocation, body, 0, body.Length);
            }
            private static NameValueCollection ExtractHeaders(string messageContent)
            {
                if (messageContent == null)
                    throw new ArgumentNullException("messageContent");

                NameValueCollection headers = new NameValueCollection();

                using (StringReader messageReader = new StringReader(messageContent))
                {
                    // Read until all headers have ended.
                    // The headers ends when an empty line is encountered
                    // An empty message might actually not have an empty line, in which
                    // case the headers end with null value.
                    string line;
                    while (!string.IsNullOrEmpty(line = messageReader.ReadLine()))
                    {
                        // Split into name and value
                        KeyValuePair<string, string> header = SeparateHeaderNameAndValue(line);

                        // First index is header name
                        string headerName = header.Key;

                        // Second index is the header value.
                        // Use a StringBuilder since the header value may be continued on the next line
                        StringBuilder headerValue = new StringBuilder(header.Value);

                        // Keep reading until we would hit next header
                        // This if for handling multi line headers
                        while (IsMoreLinesInHeaderValue(messageReader))
                        {
                            // Unfolding is accomplished by simply removing any CRLF
                            // that is immediately followed by WSP
                            // This was done using ReadLine (it discards CRLF)
                            // See http://tools.ietf.org/html/rfc822#section-3.1.1 for more information
                            string moreHeaderValue = messageReader.ReadLine();

                            // If this exception is ever raised, there is an serious algorithm failure
                            // IsMoreLinesInHeaderValue does not return true if the next line does not exist
                            // This check is only included to stop the nagging "possibly null" code analysis hint
                            if (moreHeaderValue == null)
                                throw new ArgumentException("This will never happen");

                            // Simply append the line just read to the header value
                            headerValue.Append(moreHeaderValue);
                        }

                        // Now we have the name and full value. Add it
                        headers.Add(headerName, headerValue.ToString());
                    }
                }

                return headers;
            }
            private static bool IsMoreLinesInHeaderValue(TextReader reader)
            {
                int peek = reader.Peek();
                if (peek == -1)
                    return false;

                char peekChar = (char)peek;

                // A multi line header must have a whitespace character
                // on the next line if it is to be continued
                return peekChar == ' ' || peekChar == '\t';
            }
            internal static KeyValuePair<string, string> SeparateHeaderNameAndValue(string rawHeader)
            {
                if (rawHeader == null)
                    throw new ArgumentNullException("rawHeader");

                string key = string.Empty;
                string value = string.Empty;

                int indexOfColon = rawHeader.IndexOf(':');

                // Check if it is allowed to make substring calls
                if (indexOfColon >= 0 && rawHeader.Length >= indexOfColon + 1)
                {
                    key = rawHeader.Substring(0, indexOfColon).Trim();
                    value = rawHeader.Substring(indexOfColon + 1).Trim();
                }

                return new KeyValuePair<string, string>(key, value);
            }
        }
        #endregion
    }
    #endregion
    #region ContentTransferEncoding
    /// <summary>
    /// <see cref="Enum"/> that describes the ContentTransferEncoding header field
    /// </summary>
    /// <remarks>See <a href="http://tools.ietf.org/html/rfc2045#section-6">RFC 2045 section 6</a> for more details</remarks>
    public enum ContentTransferEncoding
    {
        /// <summary>
        /// 7 bit Encoding
        /// </summary>
        SevenBit,

        /// <summary>
        /// 8 bit Encoding
        /// </summary>
        EightBit,

        /// <summary>
        /// Quoted Printable Encoding
        /// </summary>
        QuotedPrintable,

        /// <summary>
        /// Base64 Encoding
        /// </summary>
        Base64,

        /// <summary>
        /// Binary Encoding
        /// </summary>
        Binary
    }
    #endregion
}
