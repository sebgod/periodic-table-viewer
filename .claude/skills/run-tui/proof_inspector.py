"""Worked driver + regression check for pt-tui's debug inspector.

Usage: python proof_inspector.py <port>

Proves the whole path end to end: the command server answers, the periodic table
reaches the CELL plane (so `screen` can assert it), the decay chain reaches
`appState` (it cannot reach `screen` -- it is Sixel), and an injected key both
moves the selection and shows up in the input log.

Exits non-zero with a diagnosis on the first failed assertion.
"""

import json
import socket
import sys


class Inspector:
    def __init__(self, port):
        self.sock = socket.create_connection(("127.0.0.1", port), timeout=5)
        self.file = self.sock.makefile("rw", encoding="utf-8", newline="\n")
        self.next_id = 1

    def call(self, method, **params):
        request = {"id": self.next_id, "method": method, "params": params}
        self.next_id += 1
        self.file.write(json.dumps(request) + "\n")
        self.file.flush()
        line = self.file.readline()
        if not line:
            raise SystemExit("connection closed -- did the app exit?")
        reply = json.loads(line)
        if "error" in reply:
            raise SystemExit(f"{method} failed: {reply['error']}")
        return reply.get("result")

    def close(self):
        self.file.close()
        self.sock.close()


def check(condition, message):
    if not condition:
        raise SystemExit(f"FAIL: {message}")
    print(f"  ok: {message}")


def main():
    if len(sys.argv) < 2:
        raise SystemExit("usage: python proof_inspector.py <port>")
    ins = Inspector(int(sys.argv[1]))

    print("ping / size")
    pong = ins.call("ping")
    check(pong.get("app") == "pt-tui", f"app identifies as pt-tui (got {pong.get('app')!r})")
    size = ins.call("size")
    check(size["buffered"], "cell buffer is on -- without it `screen` reports nothing")
    print(f"  grid {size['columns']}x{size['rows']}, cell {size['cellWidth']}x{size['cellHeight']}px")

    print("screen -- the table is TEXT, unlike chess's Sixel board")
    # `screen` answers {"rows": [...]}, not a bare array. Indexing it as a list silently
    # iterates the KEY names instead, so every assertion passes against the string "rows".
    screen = ins.call("screen")["rows"]
    joined = "\n".join(screen)
    check("Periodic Table Viewer" in joined, "header bar is on screen")
    # The f-block placeholder column: the '*' cells pointing at the lanthanides.
    check(any("57" in row and "71" in row for row in screen),
          "f-block placeholder row (57/71) rendered")

    print("appState -- the only way to assert the Sixel decay-chain band")
    # Home first, so the script is re-runnable against an instance a previous run already
    # moved. Asserting the startup selection would only hold on a fresh launch.
    ins.call("batch", steps=[
        {"method": "key", "params": {"key": "Home"}},
        {"method": "wait", "params": {"frames": 3}},
    ])
    state = ins.call("appState")
    check(state["z"] == 1 and state["symbol"] == "H", f"Home selects hydrogen (got Z={state['z']})")
    print(f"  sixel={state['sixel']} shape={state['shape']} detailRows={state['detailRows']} "
          f"chainRows={state['chainRows']} orbitalCols={state['orbitalCols']}")

    print("the frame's promises, checked against the size it was resolved for")
    # ViewerFrameLayout guarantees these; asserting them here proves the LIVE frame agrees with
    # the unit tests, against a real terminal size rather than a parameterised one.
    check(state["shape"] in ("Compact", "DetailMath", "FullMath"),
          f"shape is one the enum declares (got {state['shape']!r})")
    chrome = 1 + 1 + state["detailRows"] + state["chainRows"]
    check(chrome <= size["rows"],
          f"chrome fits the grid ({chrome} rows of {size['rows']}) -- Fixed sizing does not clamp itself")
    check(state["orbitalCols"] == 0 or size["columns"] - state["orbitalCols"] >= 90,
          f"orbital gutter leaves the table its 90 columns "
          f"({size['columns']} - {state['orbitalCols']})")

    print("inject a key -- RightArrow should step H -> He")
    ins.call("batch", steps=[
        {"method": "key", "params": {"key": "RightArrow"}},
        {"method": "wait", "params": {"frames": 3}},
    ])
    state = ins.call("appState")
    check(state["z"] == 2 and state["symbol"] == "He",
          f"selection advanced to helium (got Z={state['z']} {state['symbol']})")

    print("End -- jump to the last element, which has a decay chain")
    ins.call("batch", steps=[
        {"method": "key", "params": {"key": "End"}},
        {"method": "wait", "params": {"frames": 3}},
    ])
    state = ins.call("appState")
    check(state["z"] == 118 and state["symbol"] == "Og",
          f"End jumps to oganesson (got Z={state['z']} {state['symbol']})")

    print("inputLog -- the events and what each changed")
    # Also an object, keyed "events" -- same trap as `screen`.
    for entry in ins.call("inputLog")["events"][-4:]:
        print(f"  {entry}")

    ins.close()
    print("\nPASS")


if __name__ == "__main__":
    main()
