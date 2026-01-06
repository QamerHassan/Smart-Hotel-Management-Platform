CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE Rooms (
    Id SERIAL PRIMARY KEY,
    RoomNumber VARCHAR(50) NOT NULL,
    RoomType VARCHAR(50) NOT NULL,
    BasePrice NUMERIC(10,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Available'
);

CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL
);

CREATE TABLE Bookings (
    Id SERIAL PRIMARY KEY,
    RoomId INT NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    CheckIn TIMESTAMP NOT NULL,
    CheckOut TIMESTAMP NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    FinalPrice NUMERIC(10,2) NOT NULL,
    
    -- Prevent overlapping bookings
    EXCLUDE USING GIST (
        RoomId WITH =,
        tstzrange(CheckIn, CheckOut) WITH &&
    )
);

CREATE INDEX idx_booking_room_dates ON Bookings(RoomId, CheckIn, CheckOut);

-- Initial Data
INSERT INTO Rooms (RoomNumber, RoomType, BasePrice) VALUES
('101', 'Single', 50.00),
('102', 'Double', 80.00),
('103', 'Suite', 150.00);

-- Users (Passwords are 'password123' hashed with BCrypt - placeholder for now)
INSERT INTO Users (Email, PasswordHash, Role) VALUES
('guest@example.com', '$2a$11$q9i8u/iRjC0k3sL.KkG4e.p4i5vK6f8/O1u4G2eR6v7k.eW6S2w2G', 'Guest'),
('reception@example.com', '$2a$11$q9i8u/iRjC0k3sL.KkG4e.p4i5vK6f8/O1u4G2eR6v7k.eW6S2w2G', 'Receptionist'),
('manager@example.com', '$2a$11$q9i8u/iRjC0k3sL.KkG4e.p4i5vK6f8/O1u4G2eR6v7k.eW6S2w2G', 'Manager'),
('admin@example.com', '$2a$11$q9i8u/iRjC0k3sL.KkG4e.p4i5vK6f8/O1u4G2eR6v7k.eW6S2w2G', 'Admin');
