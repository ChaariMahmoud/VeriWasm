pragma solidity ^0.5.0;

contract SimpleContract {
    uint x;

    function set(uint _x) public {
        x = _x;
    }

    function get() public view returns (uint) {
        return x;
    }
}
