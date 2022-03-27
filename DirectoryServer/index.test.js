const request = require("supertest");
const app = require("./server.js");

describe("Test that the server works", () => {
    test("Test GET method", async () => {
        const res = await request(app).get("/");
        expect(res.statusCode).toBe(200);
    })
})

describe("Test that registering and requesting nodes works", () => {
    test("Double as port", async() => {
        const data = {
            address: "wikipedia:3211.32",
            pubkey: "dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("Invalid as port", async() => {
        const data = {
            address: "wikipedia:dsadsa",
            pubkey: "dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("Invalid as address", async() => {
        const data = {
            address: "wikipedia:dsadsa:dsajdksjakj:8329189",
            pubkey: "dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("Empty key", async() => {
        const data = {
            address: "wikipedia:dsadsa:dsajdksjakj:8329189",
            pubkey: ""
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("Missing key", async() => {
        const data = {
            address: "wikipedia:dsadsa:dsajdksjakj:8329189"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("Missing address", async() => {
        const data = {
            pubkey: "dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(400);
    })

    test("POST new node", async () => {
        const data = {
            address: "wikipedia:3211",
            pubkey: "dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg"
        };

        const res = await request(app)
            .post("/postnode")
            .send(data);

        expect(res.statusCode).toBe(200);
    })

    test("Check that Node is registered", async () => {
        const res = await request(app).get("/");
        expect(res.body[0].address).toBe("wikipedia:3211");
        expect(res.body[0].pubkey).toBe("dksaldkslakdlsakjgfhdhgjjsdlhgjsdhg");
    })
})