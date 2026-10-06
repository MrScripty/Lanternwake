"""Contract tests with an explicit simulated Pumas boundary; no live inference claim."""
import copy
import http.client
import json
import threading
import unittest
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from bridge import BridgeError, BridgeServer, ConversationRequest, PumasGateway, loopback_endpoint


def status_fixture(provider="llama_cpp"):
    # Fields consumed here come from Pumas models/serving.rs at the documented commit.
    return {"jsonrpc": "2.0", "id": "lanternwake-status", "result": {
        "success": True, "snapshot": {"schema_version": 1, "cursor": "serving:1",
        "endpoint": {"mode": "pumas_gateway"}, "router_profiles": [], "last_errors": [],
        "served_models": [{"model_id": "llm/test/gguf", "model_alias": "lanternwake-dialogue",
                           "provider": provider, "profile_id": "game-cpu", "load_state": "loaded"}]}}}


@contextmanager
def run_server(server):
    thread = threading.Thread(target=server.serve_forever)
    thread.start()
    try:
        yield server
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


class FakePumasHandler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        payload = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        self.server.requests.append((self.path, payload))
        if self.path == "/rpc":
            value = self.server.status
        elif self.path == "/v1/chat/completions":
            value = self.server.completion
        else:
            self.send_error(404)
            return
        raw = json.dumps(value).encode()
        self.send_response(self.server.response_status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.fake = ThreadingHTTPServer(("127.0.0.1", 0), FakePumasHandler)
        self.fake.status = status_fixture()
        self.fake.completion = {"choices": [{"message": {"role": "assistant", "content": "Mind the old pier."}}]}
        self.fake.requests = []
        self.fake.response_status = 200
        self.fake_context = run_server(self.fake)
        self.fake_context.__enter__()
        self.gateway = PumasGateway(f"http://127.0.0.1:{self.fake.server_port}", "lanternwake-dialogue")
        self.bridge = BridgeServer(0, self.gateway)
        self.bridge_context = run_server(self.bridge)
        self.bridge_context.__enter__()

    def tearDown(self):
        self.bridge_context.__exit__(None, None, None)
        self.fake_context.__exit__(None, None, None)

    def request(self, value=None, path="/conversation", method="POST", headers=None, raw=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.bridge.server_port, timeout=3)
        headers = {"Content-Type": "application/json", **(headers or {})}
        raw = raw if raw is not None else json.dumps(value or {
            "character": "Mara", "context": "The harbor lantern is out.", "player_text": "What happened?"})
        try:
            connection.request(method, path, raw if method == "POST" else None, headers)
            response = connection.getresponse()
            return response.status, json.loads(response.read())
        finally:
            connection.close()

    def assert_error(self, code, **kwargs):
        status, value = self.request(**kwargs)
        self.assertNotEqual(status, 200)
        self.assertEqual(value["error"]["code"], code)

    def test_uses_verified_rpc_and_gateway_routes(self):
        status, value = self.request()
        self.assertEqual((status, value), (200, {"text": "Mind the old pier."}))
        self.assertEqual([p for p, _ in self.fake.requests], ["/rpc", "/v1/chat/completions"])
        rpc, chat = [v for _, v in self.fake.requests]
        self.assertEqual(rpc["method"], "get_serving_status")
        self.assertEqual(chat["model"], "lanternwake-dialogue")
        self.assertFalse(chat["stream"])
        self.assertEqual(chat["messages"][1], {"role": "user", "content": "What happened?"})
        self.assertNotIn("tools", chat)

    def test_health_checks_readiness_without_inference(self):
        status, value = self.request(method="GET", path="/health")
        self.assertEqual(status, 200)
        self.assertTrue(value["ready"])
        self.assertEqual([p for p, _ in self.fake.requests], ["/rpc"])

    def test_library_only_rpc_and_error_envelopes_fail_closed(self):
        self.fake.status = {"jsonrpc": "2.0", "id": "lanternwake-status", "error": {"code": -32601}}
        self.assert_error("pumas_contract")
        self.assertEqual(len(self.fake.requests), 1)

    def test_status_correlation_is_required(self):
        self.fake.status["id"] = "different-request"
        self.assert_error("pumas_contract")

    def test_other_provider_is_not_called(self):
        self.fake.status = status_fixture("ollama")
        self.assert_error("wrong_provider")
        self.assertEqual(len(self.fake.requests), 1)

    def test_missing_model_and_loading_model_fail_closed(self):
        model = self.fake.status["result"]["snapshot"]["served_models"][0]
        model["load_state"] = "loading"
        self.assert_error("model_unavailable")

    def test_ambiguous_model_fails_closed(self):
        models = self.fake.status["result"]["snapshot"]["served_models"]
        models.append(copy.deepcopy(models[0]))
        self.assert_error("model_unavailable")

    def test_stale_router_fails_closed(self):
        self.fake.status["result"]["snapshot"]["router_profiles"] = [{
            "profile_id": "game-cpu", "observation_state": "unavailable", "catalog_state": "current"}]
        self.assert_error("model_unavailable")

    def test_unknown_status_schema_is_not_accepted(self):
        self.fake.status["result"]["snapshot"]["schema_version"] = 2
        self.assert_error("pumas_contract")

    def test_model_text_is_only_text_never_game_commands(self):
        self.fake.completion["choices"][0]["message"]["content"] = '{"grant_item":"lantern"}'
        status, value = self.request()
        self.assertEqual(status, 200)
        self.assertEqual(set(value), {"text"})
        self.assertIsInstance(value["text"], str)

    def test_invalid_completion_is_rejected(self):
        for value in [None, "", 123, "x" * 4001, "bad\x00text"]:
            with self.subTest(value=str(value)[:20]):
                self.fake.completion["choices"][0]["message"]["content"] = value
                self.assert_error("invalid_upstream")

    def test_invalid_inputs_do_not_reach_pumas(self):
        for value in [[], {}, {"character": "a", "context": "b", "player_text": "c", "tool": "mutate"},
                      {"character": "a", "context": "b", "player_text": "x" * 2001},
                      {"character": "a", "context": "b", "player_text": 1}]:
            with self.subTest(value=str(value)[:60]):
                self.assert_error("invalid_request", raw=json.dumps(value))
        self.assertEqual(self.fake.requests, [])

    def test_duplicate_fields_are_rejected(self):
        self.assert_error("invalid_request", raw='{"character":"a","context":"b","player_text":"x","player_text":"y"}')
        self.assertEqual(self.fake.requests, [])

    def test_browser_origin_and_remote_host_are_rejected(self):
        self.assert_error("forbidden", headers={"Origin": "https://evil.example"})
        self.assert_error("forbidden", headers={"Host": "evil.example"})
        self.assertEqual(self.fake.requests, [])

    def test_oversized_request_is_rejected(self):
        self.assert_error("invalid_request", raw=" " * 24577)
        self.assertEqual(self.fake.requests, [])

    def test_generation_slot_is_bounded(self):
        self.gateway.generation_slot.acquire()
        try:
            self.assert_error("busy")
            self.assertEqual(self.fake.requests, [])
        finally:
            self.gateway.generation_slot.release()

    def test_provider_http_failure_is_not_fabricated_dialogue(self):
        self.fake.response_status = 503
        self.assert_error("pumas_unavailable")

    def test_closed_backend_is_unavailable(self):
        self.fake.shutdown()
        self.fake.server_close()
        self.assert_error("pumas_unavailable")


class EndpointTests(unittest.TestCase):
    def test_only_literal_loopback_without_extra_url_authority(self):
        self.assertEqual(loopback_endpoint("http://127.0.0.1:8080"), ("127.0.0.1", 8080))
        for value in ["https://127.0.0.1:8080", "http://localhost:8080", "http://example.com:8080",
                      "http://127.0.0.1:8080/rpc", "http://user:pass@127.0.0.1:8080",
                      "http://127.0.0.1:8080?x=y", "http://127.0.0.1"]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                loopback_endpoint(value)


if __name__ == "__main__":
    unittest.main()
