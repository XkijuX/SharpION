const express = require("express");
const app = express();
const port = 8080;

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
    console.log(req.body);
    const node = req.body;
    if(nodes[node.addres]) delete nodes[node.address];
    nodes[node.address] = { pubkey: node.pubkey, address: node.address };
    console.log(nodes);
    res.status(200).send("Added!");
});

app.listen(port, () => {
    console.log(`Listening on port: ${port}`);
})