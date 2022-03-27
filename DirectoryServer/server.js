const express = require("express");
const app = express();

const nodes = {};

app.use(express.json());

app.get('/', (req, res) => {
    // Sort the array randomly
    const randomlySorted = Object.values(nodes).sort(() => 0.5 - Math.random());
    console.log(nodes);
    // Respond with three nodes
    res.status(200).send(randomlySorted.slice(0, 3));
});

app.post('/postnode' , (req, res) => {
    const node = req.body;

    if(!node.address) return res.status(400).send("Bad Request: Address is required");
    const addressAndPort = node.address.split(":");

    // Check that it contains an address and port.
    if(addressAndPort.length != 2) return res.status(400).send("Bad Request: Address is invalid or not supported");
    
    // Check if port is an integer
    if(isNaN(addressAndPort[1]) || parseFloat(addressAndPort[1]) % 1 != 0) return res.status(400).send("Bad Request: Port was invalid (needs to be a whole number)");

    // Check if public key exists
    if(!node.pubkey || node.pubkey.length <= 0) return res.status(400).send("Bad Request: Key is required");

    // If node exists delete the old node
    if(nodes[node.address]) delete nodes[node.address];

    // Save new node
    nodes[node.address] = { pubkey: node.pubkey, address: node.address };

    res.status(200).send("Added!");
});

module.exports = app;