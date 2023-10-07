const ee = globalThis?.process?.versions?.node != null;
let d;
if (ee)
  d = "node@" + (await import("os")).hostname() + "@" + process.pid;
else if ("document" in globalThis)
  d = "web@" + globalThis.document.location + "@" + Date.now().toString(36) + "X" + Math.random().toString(36).substring(2);
else
  throw new Error("Unknown Platform");
const M = /* @__PURE__ */ Object.create(null), y = /* @__PURE__ */ new Map();
y.set("$" + d, M);
async function te(t, e) {
  if (!y.has(t) && (y.set(t, e), _))
    try {
      await w(null, "+", t);
    } catch (n) {
      console.log(n);
    }
}
async function ue(t) {
  if (y.has(t)) {
    if (_)
      try {
        await w(null, "-", t);
      } catch (e) {
        console.log(e);
      }
    y.delete(t);
  }
}
class X {
  [Symbol.toStringTag] = "PendingCall";
  finished = !1;
  promise;
  constructor() {
    this.promise = new Promise((e, n) => {
      I.set(this, (r) => {
        e(r), this.finished = !0;
      }), g.set(this, (r) => {
        n(r), this.finished = !0;
      });
    });
  }
  catch(e) {
    return this.promise.catch(e);
  }
  finally(e) {
    return this.promise.finally(e);
  }
  then(e, n) {
    return this.promise.then(e, n);
  }
  // noinspection JSUnusedLocalSymbols
  sendMessage(...e) {
    return this;
  }
  addMessageListener(e) {
    return U(this, e), this;
  }
  cancel() {
  }
  //overridden by callFunction and callLocal
  [Symbol.asyncIterator]() {
    return j(this);
  }
}
function j(t) {
  let e = [], n = [], r = t.finished;
  return t.promise.finally(() => {
    r = !0;
    for (let i of n)
      i(void 0);
  }), t.addMessageListener((...i) => e.push(i)), {
    async next() {
      if (r || t.finished)
        return { done: !0, value: void 0 };
      if (e.length)
        return { done: !1, value: e.shift() };
      const i = await new Promise((s) => n.push(s));
      return i == null ? { done: !0, value: void 0 } : { done: !1, value: i[0] };
    }
  };
}
const I = /* @__PURE__ */ new WeakMap(), g = /* @__PURE__ */ new WeakMap(), L = /* @__PURE__ */ new WeakMap(), S = /* @__PURE__ */ new WeakMap();
function U(t, e) {
  if (S.has(t))
    S.get(t).push(e);
  else {
    S.set(t, [e]);
    const n = L.get(t) ?? [];
    for (let r of n)
      try {
        e(...r);
      } catch (i) {
        console.warn("Error receiving pending: " + i);
      }
  }
}
function A(t, e) {
  if (!t.finished)
    if (S.has(t))
      for (let n of S.get(t))
        try {
          n(...e);
        } catch (r) {
          console.warn("Error receiving: " + r);
        }
    else
      L.has(t) ? L.set(t, [...L.get(t), e]) : L.set(t, [e]);
}
let v = null;
function ne(t, e) {
  const n = v;
  v = e;
  try {
    return t();
  } finally {
    v = n;
  }
}
function ge() {
  if (v == null)
    throw new Error("FunctionCallContext not available");
  return v;
}
let fe = 0;
function w(t, e, ...n) {
  if (t != null) {
    const c = y.get(t);
    if (c)
      return re(t, e, () => c[e](...n));
  }
  const r = new X(), i = [];
  r.finally(() => {
  });
  const s = new p(), o = fe++;
  try {
    s.writeByte(T.FunctionCall), s.writeLength(o), s.writeString(t), s.writeString(e), s.writeArray(n, (c) => s.writeDynamic(c, i));
  } catch (c) {
    return g.get(r)?.(c), r;
  }
  return _ || t == null && F != null ? (r.sendMessage = (...c) => {
    if (r.finished)
      return r;
    const a = new p();
    a.writeByte(T.MessageToExecutor), a.writeLength(o);
    const b = [];
    return a.writeArray(c, (l) => a.writeDynamic(l, i)), i.push(...b), E(a), r;
  }, r.cancel = () => {
    if (r.finished)
      return;
    const c = new p();
    c.writeByte(T.FunctionCancel), c.writeLength(o), E(c);
  }, be(o, r, s)) : g.get(r)?.(new Error("Not connected")), r;
}
function we(t) {
  return re(null, null, t);
}
function re(t, e, n) {
  const r = new X(), i = new AbortController(), s = {
    type: t,
    method: e,
    sendMessage: (...o) => (r.finished || A(r, o), s),
    get finished() {
      return r.finished;
    },
    promise: r,
    addMessageListener: (o) => (U(s, o), s),
    cancelToken: i.signal,
    cancel: () => i.abort(),
    [Symbol.asyncIterator]: () => j(s)
  };
  r.sendMessage = (...o) => (r.finished || A(s, o), r), r.cancel = () => r.finished || s.cancel();
  try {
    const o = ne(n, s);
    o instanceof Promise ? o.then((c) => I.get(r)?.(c), (c) => g.get(r)?.(c)) : I.get(r)?.(o);
  } catch (o) {
    g.get(r)?.(o);
  }
  return r;
}
function $(t) {
  return "type" in t && "method" in t;
}
function x(t, e) {
  return Object.freeze(Object.assign(
    (...n) => w(t, e, ...n),
    {
      type: t,
      method: e,
      toString: () => `rpc (...params) => ${t ?? "null"}.${e}(...params)`
    }
  ));
}
let de = Date.now();
function z(t) {
  if ($(t))
    return t;
  const e = (de++).toString(16);
  M[e] = t;
  const n = "$" + d;
  return Object.assign(t, {
    //TODO functioncontext
    type: n,
    method: e,
    toString: () => `rpc (...params) => ${n ?? "null"}.${e}(...params)`
  });
}
function ie(t) {
  if (!$(t))
    return;
  const e = "$" + d;
  if (t.type != e)
    throw new Error("Can't unregister RemoteFunction, that was not registered locally");
  const n = M[t.method];
  delete M[t.method], delete n.type, delete n.method;
}
class R extends Error {
  name;
  from;
  stackTrace;
  constructor(e, n, r, i) {
    typeof i == "string" ? (super(r), this.stackTrace = i) : i ? (super(r, { cause: i }), this.stackTrace = this.stack.substring(this.stack.indexOf(`
`) + 1) + `
caused by: ` + i.stack) : (super(r), this.stackTrace = this.stack.substring(this.stack.indexOf(`
`) + 1)), this.name = e ?? "UnknownRemoteError", this.from = n, this.stack = this.name + "(" + n + ")", r != null && (this.stack += ": " + r), i != null && (this.stack += `
`, this.stackTrace = this.stackTrace.replaceAll(/^  +/gm, "	"), this.stack += this.stackTrace);
  }
}
const O = Symbol("RemoteObjectType");
function N(t, e = {}) {
  const n = /* @__PURE__ */ new Map();
  return new Proxy(e, {
    get(r, i) {
      if (i == O)
        return t;
      if (typeof i != "string" || i == "then")
        return e[i];
      if (n.has(i))
        return n.get(i);
      const s = x(
        t,
        i
      );
      return n.set(i, s), s;
    },
    construct(r, i) {
      return new r(...i);
    }
  });
}
const se = new Proxy({}, {
  get: (t, e) => typeof e == "string" ? N(e) : void 0,
  has: (t, e) => !(e in globalThis) && e != "then"
}), oe = [], ce = /* @__PURE__ */ new Map();
function q(t, e) {
  let n = t.readLength();
  if (n < 0) {
    switch (n = -n, n % 4) {
      case 0:
        return e[n / 4];
      case 1:
        return new TextDecoder().decode(t.readBuffer((n - 1) / 4));
      case 2: {
        const r = {};
        e.push(r);
        for (let i = 0; i < n / 4; i++) {
          const s = t.readString();
          r[s] = q(t, e);
        }
        return r;
      }
      case 3: {
        const r = new Array((n - 3) / 4);
        e.push(r);
        for (let i = 0; i < r.length; i++)
          r[i] = q(t, e);
        return r;
      }
    }
    throw new Error("Unreachable code reached");
  } else if (n >= 128) {
    const r = new TextDecoder().decode(t.readBuffer(n - 128)), i = ce.get(r);
    if (i)
      return i(t, e);
    throw new Error("Unknown data type: " + r);
  } else
    switch (String.fromCodePoint(n)) {
      case "n":
        return null;
      case "t":
        return !0;
      case "f":
        return !1;
      case "i":
        return t.readInt();
      case "d":
        return t.readDouble();
      case "l":
        return t.readLong();
      case "b":
        return t.readBuffer(t.readLength());
      case "D":
        return new Date(Number(t.readLong()));
      case "R": {
        const r = t.readString(), i = t.readByte();
        return new RegExp(
          r,
          "g" + (i & 1 ? "i" : "") + (i & 2 ? "m" : "")
        );
      }
      case "E":
        return t.readError();
      case "O": {
        const r = t.readString();
        return N(r);
      }
      case "F": {
        const r = t.readString(), i = t.readString();
        if (i == null)
          throw new Error("InvalidOperation");
        return x(r, i);
      }
      default:
        throw new Error("Unknown data type number: " + n);
    }
}
function V(t, e, n) {
  if (e == null)
    t.writeLength("n".charCodeAt(0));
  else if (e === !0)
    t.writeLength("t".charCodeAt(0));
  else if (e === !1)
    t.writeLength("f".charCodeAt(0));
  else if (e === +e)
    t.writeLength("i".charCodeAt(0)), t.writeInt(e);
  else if (typeof e == "number")
    t.writeLength("d".charCodeAt(0)), t.writeDouble(e);
  else if (typeof e == "bigint")
    t.writeLength("l".charCodeAt(0)), t.writeLong(e);
  else if (e instanceof Uint8Array)
    t.writeLength("b".charCodeAt(0)), t.writeLength(e.length), t.writeBuffer(e);
  else if (e instanceof Date)
    t.writeLength("D".charCodeAt(0)), t.writeLong(+e);
  else if (e instanceof RegExp) {
    t.writeLength("R".charCodeAt(0)), t.writeString(e.source);
    const r = e.flags;
    t.writeByte(
      (r.includes("i") ? 1 : 0) || (r.includes("m") ? 2 : 0)
    );
  } else if (e instanceof Error)
    t.writeLength("E".charCodeAt(0)), t.writeError(e);
  else if (e[O] != null)
    t.writeLength("O".charCodeAt(0)), t.writeString(e[O]);
  else if (typeof e == "function")
    t.writeLength("F".charCodeAt(0)), t.writeFunction(e);
  else if (n.includes(e))
    t.writeLength(-(n.indexOf(e) * 4));
  else if (typeof e == "string") {
    const r = new TextEncoder().encode(e);
    t.writeLength(-(r.length * 4 + 1)), t.writeBytes(r);
  } else if (Array.isArray(e)) {
    n.push(e), t.writeLength(-(e.length * 4 + 3));
    for (let r of e)
      V(t, r, n);
  } else {
    for (let [r, i, s] of oe) {
      if (!i(e))
        continue;
      const o = new TextEncoder().encode(r);
      t.writeLength(o.length + 128), t.writeBytes(o), s(t, e, n);
      return;
    }
    if (typeof e == "object") {
      n.push(e);
      const r = Object.entries(e);
      t.writeLength(-(r.length * 4 + 2));
      for (let [i, s] of r)
        t.writeString(i), V(t, s, n);
    } else
      throw new Error("Unknown type for " + e);
  }
}
class p {
  _buf;
  _data;
  _count = 0;
  functions = [];
  constructor(e = 32) {
    this._buf = typeof e == "number" ? new Uint8Array(e) : e, this._data = new DataView(this._buf.buffer);
  }
  ensureCapacity(e) {
    if (e += this._count, e > this._buf.byteLength) {
      let n = new Uint8Array(Math.max(this._buf.byteLength * 2, e));
      this._data = new DataView(n.buffer), n.set(this._buf), this._buf = n;
    }
  }
  writeByte(e) {
    this.ensureCapacity(1), this._buf[this._count] = e, this._count++;
  }
  writeBytes(e) {
    this.ensureCapacity(e.length), this._buf.set(e, this._count), this._count += e.length;
  }
  writeBuffer(e) {
    this.writeBytes(e);
  }
  writeBoolean(e) {
    this.writeByte(e ? 1 : 0);
  }
  writeNullBoolean(e) {
    this.writeByte(e == null ? 2 : e ? 1 : 0);
  }
  writeShort(e) {
    this.ensureCapacity(2), this._data.setInt16(this._count, e), this._count += 2;
  }
  writeChar(e) {
    this.writeShort(e.charCodeAt(0));
  }
  writeInt(e) {
    this.ensureCapacity(4), this._data.setInt32(this._count, e), this._count += 4;
  }
  writeLong(e) {
    typeof e == "number" ? (this.writeInt(e / 2 ** 32), this.writeInt(e % 2 ** 32)) : (this.writeInt(Number(e / BigInt(2 ** 32))), this.writeInt(Number(e % BigInt(2 ** 32))));
  }
  writeFloat(e) {
    this.ensureCapacity(4), this._data.setFloat32(this._count, e), this._count += 4;
  }
  writeDouble(e) {
    this.ensureCapacity(8), this._data.setFloat64(this._count, e), this._count += 8;
  }
  writeString(e) {
    if (e == null) {
      this.writeLength(-1);
      return;
    }
    let n = new TextEncoder().encode(e);
    this.writeLength(n.length), this.writeBytes(n);
  }
  writeLength(e) {
    let n = (e < 0 ? ~e : e) >>> 0;
    for (; n >= 128; )
      this.writeByte(n | 128), n >>= 7;
    e < 0 ? (this.writeByte(n | 128), this.writeByte(0)) : this.writeByte(n);
  }
  writeByteArray(e) {
    e ? (this.writeLength(e.length), this.writeBytes(e)) : this.writeLength(-1);
  }
  writeArray(e, n) {
    if (!e)
      this.writeLength(-1);
    else {
      this.writeLength(e.length);
      for (let r = 0; r < e.length; r++)
        n.call(this, e[r]);
    }
  }
  toBuffer(e = 0) {
    return this._buf.slice(e, this._count - e);
  }
  writeFunction(e) {
    $(e) || (e = z(e), this.functions.push(e)), this.writeString(e.type), this.writeString(e.method);
  }
  writeError(e) {
    const n = e instanceof R ? e : new R(e.name, u, e.message, e.stack);
    this.writeString(n.name), this.writeString(n.from), this.writeString(n.message), this.writeString(n.stackTrace);
  }
  /*writeError(error: Error){
  	const remote=error instanceof RemoteError?
  		error:
  		new UserRemoteError(error);
  	remote.writeError(this);
  }*/
  writeObject(e, n) {
    n.push(e);
    for (const r in e)
      this.writeString(r), this.writeDynamic(e[r], n);
    this.writeString(null);
  }
  writeDynamic(e, n = []) {
    V(this, e, n);
  }
}
const k = /* @__PURE__ */ new Map(), C = /* @__PURE__ */ new Map();
function ye(t) {
  for (let e of k.values())
    g.get(e)?.(t);
  k.clear();
  for (let e of C.values())
    e.cancel();
}
function E(t) {
  if (F == null)
    throw new Error("Not connected");
  F.send(t.toBuffer());
}
function be(t, e, n) {
  k.set(t, e);
  try {
    E(n);
  } catch (r) {
    g.get(e)?.(r);
  }
}
var T = /* @__PURE__ */ ((t) => (t[t.FunctionCall = 0] = "FunctionCall", t[t.FunctionSuccess = 1] = "FunctionSuccess", t[t.FunctionError = 2] = "FunctionError", t[t.FunctionCancel = 3] = "FunctionCancel", t[t.MessageToExecutor = 4] = "MessageToExecutor", t[t.MessageToCaller = 5] = "MessageToCaller", t))(T || {});
let D = !1;
globalThis.process ? process.on("unhandledRejection", () => {
  D = !1;
}) : window.addEventListener("unhandledrejection", (t) => {
  D && t.reason instanceof R && (D = !1, t.preventDefault());
});
async function me(t) {
  try {
    switch (t.readByte()) {
      case 0: {
        const n = t.readLength(), r = [];
        let i = !1, s = null, o = null;
        const c = new Promise((a, b) => {
          s = (l) => {
            a(l), i = !0;
            const h = new p();
            h.writeByte(
              1
              /* FunctionSuccess */
            ), h.writeLength(n), h.writeDynamic(l), E(h), C.delete(n);
          }, o = (l) => {
            b(l), i = !0;
            const h = new p();
            h.writeByte(
              2
              /* FunctionError */
            ), h.writeLength(n), h.writeError(l), E(h), C.delete(n);
          };
        });
        try {
          const a = t.readString();
          if (a == null)
            throw new Error("Client can't use null as a type for function calls");
          const b = y.get(a);
          if (!b)
            throw new Error(`Type "${a}" is not registered on client ${u}`);
          const l = t.readString(), h = t.readArray(() => t.readDynamic(r)) ?? [], Q = new AbortController(), m = {
            type: a,
            method: l,
            get finished() {
              return i;
            },
            promise: c,
            sendMessage(...f) {
              if (i)
                return m;
              const B = new p();
              B.writeByte(
                5
                /* MessageToCaller */
              ), B.writeLength(n);
              const Y = [];
              return B.writeArray(f, (he) => B.writeDynamic(he, Y)), r.push(...Y), E(B), m;
            },
            addMessageListener(f) {
              return U(m, f), m;
            },
            cancelToken: Q.signal,
            cancel: () => Q.abort(),
            [Symbol.asyncIterator]: () => j(m)
          };
          C.set(n, m);
          const W = ne(() => {
            const f = b[l];
            if (f == null)
              throw new Error(`Method "${l}" not found in "${a}"`);
            return f.call(b, ...h);
          }, m);
          W instanceof Promise ? W.then((f) => s(f), (f) => o(f)) : s(W);
        } catch (a) {
          o(a);
        }
        break;
      }
      case 1: {
        const n = t.readLength(), r = k.get(n);
        if (r == null) {
          console.log(`${u} has no activeRequest with id: ${n}`);
          break;
        }
        try {
          I.get(r)?.(t.readDynamic());
        } catch (i) {
          g.get(r)?.(i);
        }
        break;
      }
      case 2: {
        const n = t.readLength(), r = k.get(n);
        if (r == null) {
          console.log(`${u} has no activeRequest with id: ${n}`);
          break;
        }
        try {
          D = !0, g.get(r)?.(await t.readError());
        } catch (i) {
          g.get(r)?.(i);
        } finally {
        }
        break;
      }
      case 3: {
        const n = t.readLength();
        let r = C.get(n);
        if (!r) {
          console.log(`${u} has no CurrentlyExecuting with id: {callId}`);
          break;
        }
        r.cancel();
        break;
      }
      case 4: {
        const n = t.readLength();
        let r = C.get(n);
        if (!r) {
          console.log(`${u} has no CurrentlyExecuting with id: {callId}`);
          break;
        }
        const i = [], s = t.readArray(() => t.readDynamic(i)) ?? [];
        A(r, s);
        break;
      }
      case 5: {
        const n = t.readLength();
        let r = k.get(n);
        if (!r) {
          console.log(`${u} has no ActiveRequest with id: {callId}`);
          break;
        }
        const i = [], s = t.readArray(() => t.readDynamic(i)) ?? [];
        A(r, s);
        break;
      }
    }
  } catch (e) {
    console.error(e);
  }
}
class ae {
  _buf;
  _data;
  _pos;
  _count;
  constructor(e, n = 0, r = e.length) {
    this._buf = e, this._data = new DataView(e.buffer), this._pos = n, this._count = n + r;
  }
  readFully(e, n = 0, r = e.length) {
    let i = this._pos;
    if (this._count - i < r)
      throw new RangeError("not enough bytes available to use readFully");
    for (let o = n; o < n + r; o++)
      e[o] = this._buf[i++];
    this._pos = i;
  }
  skip(e) {
    let n = this.available();
    return e < n && (n = e < 0 ? 0 : e), this._pos += n, n;
  }
  available() {
    return this._count - this._pos;
  }
  readAll() {
    return this._buf.slice(this._pos, this._pos = this._count);
  }
  readBuffer(e) {
    if (e > this.available())
      throw new RangeError();
    return this._buf.slice(this._pos, this._pos += e);
  }
  readByte() {
    return this._data.getUint8(this._pos++);
  }
  readBoolean() {
    return this.readByte() != 0;
  }
  readNullBoolean() {
    const e = this.readByte();
    return e < 2 ? e == 1 : null;
  }
  readShort() {
    const e = this._data.getInt16(this._pos);
    return this._pos += 2, e;
  }
  readUShort() {
    const e = this._data.getUint16(this._pos);
    return this._pos += 2, e;
  }
  readChar() {
    return String.fromCharCode(this.readUShort());
  }
  readInt() {
    const e = this._data.getInt32(this._pos);
    return this._pos += 4, e;
  }
  readLong() {
    return BigInt(this.readInt()) * BigInt(2 ** 32) + BigInt(this.readInt() >>> 0);
  }
  readFloat() {
    const e = this._data.getFloat32(this._pos);
    return this._pos += 4, e;
  }
  readDouble() {
    const e = this._data.getFloat64(this._pos);
    return this._pos += 8, e;
  }
  readString() {
    let e = this.readLength();
    return e == -1 ? null : new TextDecoder().decode(this.readBuffer(e));
  }
  readLength() {
    let e = 0, n = 0;
    for (; ; ) {
      const r = this.readByte();
      if (r == 0)
        return n == 0 ? 0 : ~e;
      if (!(r & 128))
        return e |= r << n, e;
      e |= (r & 127) << n, n += 7;
    }
  }
  readArray(e) {
    const n = this.readLength();
    if (n == -1)
      return null;
    const r = [];
    for (let i = 0; i < n; i++)
      r[i] = e.call(this);
    return r;
  }
  readFunction() {
    return x(this.readString(), this.readString());
  }
  readError() {
    return new R(this.readString(), this.readString() ?? "???", this.readString(), this.readString());
  }
  readObject(e) {
    const n = {};
    e.push(n);
    for (let r = this.readString(); r != null; r = this.readString())
      n[r] = this.readDynamic(e);
    return n;
  }
  readDynamic(e = []) {
    return q(this, e);
  }
}
let _ = !1, G, J, le = new Promise((t, e) => [G, J] = [t, e]);
async function pe() {
  for (; ; )
    if (await le.then(() => !0, () => !1))
      return;
}
let K;
if (ee) {
  const t = (await import("ws")).WebSocket;
  K = () => new t(process.env.RPC_URL, {
    headers: {
      Cookie: "token=" + process.env.RPC_TOKEN
    }
  });
} else if ("document" in globalThis)
  K = () => new WebSocket("ws" + globalThis.document.location.origin.substring(4) + "/rpc");
else
  throw new Error("Unknown Platform");
function Z(t) {
  const e = J;
  le = new Promise((n, r) => [G, J] = [n, r]), e(t), ye(t);
}
let F = null;
(function t() {
  const e = K();
  e.onclose = () => {
    F = null, _ = !1, console.log("Websocket disconnected");
    const n = new Error("Connection closed");
    Z(n), setTimeout(t, 1e3);
  }, e.onopen = async () => {
    console.log("Websocket connected");
    try {
      F = e, await w(null, "N", u), await w(null, "+", ...y.keys()), _ = !0, G();
    } catch (n) {
      console.error(n.stack), Z(n), e?.close(4e3, "Error registering types");
      return;
    }
  }, e.binaryType = "arraybuffer", e.onmessage = function(r) {
    const i = r.data;
    typeof i == "string" ? console.log(i) : me(new ae(new Uint8Array(i)));
  };
})();
let P, u = d;
async function _e(t) {
  P = t, u = P != null ? `${P} (${d})` : d;
  try {
    _ && await w(null, "N", u);
  } catch (e) {
    console.error(e);
  }
}
function Ce(t) {
  return function(e) {
    te(t ?? e.prototype.constructor.name, e).catch(console.error);
  };
}
function Le(t) {
  return function(e) {
    oe.push([t, (n) => n instanceof e, (n, r, i) => r.write(n, i)]), ce.set(t, (n, r) => e.read(n, r));
  };
}
Promise.resolve().then(() => Se).then((t) => Object.assign(globalThis, t));
class H {
  //Rpc
  static id = d;
  static get nameOrId() {
    return u;
  }
  static setName(e) {
    return _e(e);
  }
  //Connection	
  static get isConnected() {
    return _;
  }
  static get waitUntilConnected() {
    return pe();
  }
  //Functions
  static createRemoteObject = N;
  static createRemoteFunction = x;
  static registerFunction = z;
  static unregisterFunction = ie;
  static callLocal = we;
  //Call function and get a PendingCall, this allows the use of the FunctionCallContext within the function
  static callFunction = w;
  //Call remote function
  static getContext = ge;
  static registerType = te;
  static unregisterType = ue;
  static checkTypes = async (...e) => await w(null, "?", ...e);
  static checkType = async (e) => await H.checkTypes(e) != 0;
  static getAllTypes = async () => await w(null, "T");
  static getAllConnections = async () => await w(null, "C");
  static objects = se;
}
const Se = /* @__PURE__ */ Object.freeze(/* @__PURE__ */ Object.defineProperty({
  __proto__: null,
  CustomDynamicType: Le,
  DataInput: ae,
  DataOutput: p,
  PendingCall: X,
  RPC_ROOT: se,
  Rpc: H,
  RpcError: R,
  RpcObjectType: O,
  RpcProvider: Ce,
  createRemoteFunction: x,
  createRpcObject: N,
  getAsyncIterator: j,
  isRemoteFunction: $,
  listenersMap: S,
  pendingMap: L,
  registerFunction: z,
  registerReceive: U,
  rejectCall: g,
  resolveCall: I,
  runReceiveMessage: A,
  unregisterFunction: ie
}, Symbol.toStringTag, { value: "Module" }));
export {
  Le as CustomDynamicType,
  ae as DataInput,
  p as DataOutput,
  X as PendingCall,
  se as RPC_ROOT,
  H as Rpc,
  R as RpcError,
  O as RpcObjectType,
  Ce as RpcProvider,
  x as createRemoteFunction,
  N as createRpcObject,
  j as getAsyncIterator,
  $ as isRemoteFunction,
  S as listenersMap,
  L as pendingMap,
  z as registerFunction,
  U as registerReceive,
  g as rejectCall,
  I as resolveCall,
  A as runReceiveMessage,
  ie as unregisterFunction
};
//# sourceMappingURL=rpc.js.map
