-- =======================================
-- DOMAINS
-- =======================================

CREATE DOMAIN PHONE_DOM AS VARCHAR(20);


-- =======================================
-- TABLES
-- =======================================

CREATE TABLE CUSTOMERS (
  ID INTEGER NOT NULL,
  NAME VARCHAR(100),
  PHONE VARCHAR(20)
);

CREATE TABLE ORDERS (
  ID INTEGER NOT NULL,
  CUSTOMER_ID INTEGER,
  ORDER_DATE DATE
);



-- =======================================
-- PROCEDURES
-- =======================================

BEGIN
  -- nic
END

