/*
 * File: Boethin.Net.DnsTools.Resolution/Internal/QueryException.cs
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

namespace Boethin.Net.DnsTools.Resolution.Internal
{
  [Serializable] 
  internal class QueryException : System.Exception
  {

    public readonly QueryState State;

    public QueryException(QueryState state)
      : base()
    {
      this.State = state;
    }

    public QueryException(QueryState state, string message)
      : base(message)
    {
      this.State = state;
    }

  }
}