/*
 * File: Boethin.Net.DnsTools.Resolution/Resolver.cs
 *
 * This file is part of Boethin.Net.DnsTools, a DNS debugging library.
 * Copyright (C) 2013 Sebastian Boethin <sebastian@boethin.eu>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Boethin.Net.DnsTools.DnsClient.DNS;
using Boethin.Net.DnsTools.DnsClient;
using Boethin.Net.DnsTools.DnsClient.DNS.Records;

namespace Boethin.Net.DnsTools.Resolution
{

  /// <summary>
  /// An iterative name resolver.
  /// </summary>
  [Serializable]
  public class DomainResolver : IDisposable
  {

    #region private

    private readonly DnsDomain _Domain;

    private readonly Options _Options;

    // The client cannot be made serializable since it's derived from either
    // System.Net.Sockets.UdpClient or System.Net.Sockets.TcpClient.
    [NonSerialized]
    private IDnsClient _Client = null;

    #endregion

    #region internal

    #endregion

    #region public get

    public Options Options
    {
      get { return _Options; }
    }

    /// <summary>
    /// The name to be resolved.
    /// </summary>
    public DnsDomain Domain
    {
      get { return _Domain; }
    }

    /// <summary>
    /// The client object associated with the current instance.
    /// <para>Use the Connect method to connect the resolver with a client.</para>
    /// </summary>
    public IDnsClient Client
    {
      get { return _Client; }
    }

    /// <summary>
    /// Whether or not the current instance is connected with a client.
    /// <para>Note that the connection with a client gets lost during serialization.</para>
    /// </summary>
    public bool Connected
    {
      get { return !object.ReferenceEquals(null, Client); }
    }

    #endregion

    #region c'tor

    /// <summary>
    /// Initialize a new instance of the Resolver class.
    /// </summary>
    /// <param name="options">Resolution options</param>
    /// <param name="domain">The domain in question</param>
    public DomainResolver(Options options, DnsDomain domain)
    {
      if (object.ReferenceEquals(null, options))
        throw new ArgumentNullException("options");
      if (object.ReferenceEquals(null, domain))
        throw new ArgumentNullException("domain");

      _Options = options;
      _Domain = domain;
    }

    /// <summary>
    /// Initialize a new instance of the Resolver class.
    /// </summary>
    /// <param name="options">Resolution options</param>
    /// <param name="domain">The domain in question</param>
    public DomainResolver(Options options, string domain)
    {
      if (object.ReferenceEquals(null, options))
        throw new ArgumentNullException("options");
      if (String.IsNullOrEmpty(domain))
        throw new ArgumentNullException("domain", "String value cannot be null or empty.");

      _Options = options;
      _Domain = (DnsDomain)domain;
    }

    /// <summary>
    /// Initialize a new instance of the Resolver class using default options.
    /// </summary>
    /// <param name="domain">The domain in question</param>
    public DomainResolver(DnsDomain domain)
    {
      if (object.ReferenceEquals(null, domain))
        throw new ArgumentNullException("domain");

      _Options = Options.Default;
      _Domain = domain;
    }

    /// <summary>
    /// Initialize a new instance of the Resolver class using default options.
    /// </summary>
    /// <param name="domain">The domain in question</param>
    public DomainResolver(string domain)
    {
      if (String.IsNullOrEmpty(domain))
        throw new ArgumentNullException("domain", "String value cannot be null or empty.");

      _Options = Options.Default;
      _Domain = (DnsDomain)domain;
    }


    internal DomainResolver(DomainResolver resolver, DnsDomain domain)
    {
      _Options = resolver.Options;
      _Domain = domain;
    }

    #endregion

    #region IDisposable

    public virtual void Dispose()
    {
      if (!Object.ReferenceEquals(null, _Client))
      {
        _Client.Dispose();
        _Client = null;
      }
    }

    #endregion

    #region public

    /// <summary>
    /// Associate a client to the resolver.
    /// <para>Note that the connection with a client gets lost during serialization.</para>
    /// </summary>
    /// <param name="client"></param>
    public void Connect(IDnsClient client)
    {
      if (object.ReferenceEquals(null, client))
        throw new ArgumentNullException("client");

      _Client = client;
    }

    /// <summary>
    /// Create a new request iterator for resolving the specified question.
    /// </summary>
    /// <param name="question">The question to resolve.</param>
    /// <returns></returns>
    public Iterators.ResolutionIterator GetIterator(DnsClient.DNS.QTYPE question)
    {
      return new Iterators.ResolutionIterator(this, question, new Caching.AddressCache());
    }

    //public Iterators.RequestIterator GetIterator(DnsClient.DNS.QTYPE question)
    //{
    //  return new Iterators.RequestIterator(this, question);
    //}

    public NameServerCollection GetNameServers(out bool isAuthoritative)
    {
      Results.ResolutionResult lastResult = null, lastNonEmptyResult = null;
      using (Iterators.ResolutionIterator iterator = GetIterator(QTYPE.NS))
      {
        while (iterator.MoveNext())
        {
          lastResult = iterator.Current;
          if (!lastResult.Authorities.IsEmpty)
            lastNonEmptyResult = lastResult;
        }
      }
      if (object.ReferenceEquals(null, lastResult) ||
          object.ReferenceEquals(null, lastNonEmptyResult))
        throw new ResolverException("Resolution failed.");
      switch (lastResult.Response.Header.RCODE)
      {
        case RCODE.NoError:
          isAuthoritative = lastResult.Response.AnswerRecords.Where(rr => null != rr as NS).Any();
          return lastNonEmptyResult.Authorities;

        case RCODE.NameError: // Non-existent domain
          isAuthoritative = true;
          return null;

        default: // failure
          throw new ResolverException(String.Format(
            "Unexpected RCODE {0} ({1}) from {2}.",
            lastResult.Response.Header.RCODE.ToString(),
            lastResult.Response.Header.RCODEValue,
            lastResult.Authorities.Selected));
      }

    }

    /// <summary>
    /// Check whether or not there are MX or Address records associated with the given name.
    /// </summary>
    /// <returns></returns>
    public bool CanMailExchange()
    {
      Results.ResolutionResult lastResult = null;
      using (Iterators.ResolutionIterator iterator = GetIterator(QTYPE.ANY))
      {
        while (iterator.MoveNext())
        {
          lastResult = iterator.Current;
        }
      }
      if (object.ReferenceEquals(null, lastResult))
        throw new ResolverException("Resolution failed.");
      switch (lastResult.Response.Header.RCODE)
      {
        case RCODE.NoError:
          // check for Address or MX records
          return lastResult.Response.AnswerRecords.Where(
            rr => typeof(Address).IsAssignableFrom(rr.GetType()) ||
              typeof(MX).IsAssignableFrom(rr.GetType())).Any();

        case RCODE.NameError: // Non-existent domain
          return false;

        default: // failure
          throw new ResolverException(String.Format(
            "Unexpected RCODE {0} ({1}) from {2}.",
            lastResult.Response.Header.RCODE.ToString(),
            lastResult.Response.Header.RCODEValue,
            lastResult.Authorities.Selected));
      }
    }

    #endregion

    #region private

    public Response LookUp(IPAddress address, QTYPE qtype, out Request request)
    {
      if (object.ReferenceEquals(null, Client))
        throw new InvalidOperationException(
          "A client must be associated with the resolver (use the Connect method).");

      Response response;

      //if (LookUpProcessing != null)
      //  LookUpProcessing(this, new NameServerEventArgs(server, qtype, _Domain));

      //IPAddress address = server.GetFirstAddress(_Options.IPVersion);

      ushort id = (ushort)new Random(DateTime.Now.Millisecond).Next(0, ushort.MaxValue);
      request = new Request(id, false, OPCODE.QUERY, new Question(_Domain.ToString(), qtype));

      try
      {
        _Client.Connect(address);
        response = _Client.Process(request);
        if (response.Header.ID != id) // paranoid
          throw new ResolverException(
            "Did not get my request ID back. Either the name server or the client software is buggy.");
      }
      finally
      {
        _Client.Close();
      }

      return response;
    }

    #endregion

  }
}
