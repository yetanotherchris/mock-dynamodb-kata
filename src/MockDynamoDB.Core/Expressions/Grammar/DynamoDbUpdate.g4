grammar DynamoDbUpdate;

// Parser rules

updateExpression
    : clause+ EOF
    ;

clause
    : setClause
    | removeClause
    | addClause
    | deleteClause
    ;

setClause
    : SET setAction (COMMA setAction)*
    ;

setAction
    : documentPath EQ setValue
    ;

setValue
    : operand (PLUS | MINUS) operand    # ArithmeticValue
    | operand                            # SingleValue
    ;

removeClause
    : REMOVE documentPath (COMMA documentPath)*
    ;

addClause
    : ADD addAction (COMMA addAction)*
    ;

addAction
    : documentPath operand
    ;

deleteClause
    : DELETE deleteAction (COMMA deleteAction)*
    ;

deleteAction
    : documentPath operand
    ;

operand
    : documentPath
    | valuePlaceholder
    | function
    ;

documentPath
    : pathElement (DOT pathElement | LBRACKET NUMBER RBRACKET)*
    ;

pathElement
    : IDENTIFIER
    | NAME_PLACEHOLDER
    ;

valuePlaceholder
    : PLACEHOLDER
    ;

function
    : functionName LPAREN operand (COMMA operand)* RPAREN
    ;

functionName
    : IF_NOT_EXISTS
    | LIST_APPEND
    ;

// Lexer rules

// Keywords (case-insensitive)
SET             : [Ss][Ee][Tt] ;
REMOVE          : [Rr][Ee][Mm][Oo][Vv][Ee] ;
ADD             : [Aa][Dd][Dd] ;
DELETE          : [Dd][Ee][Ll][Ee][Tt][Ee] ;

// Function names
IF_NOT_EXISTS   : 'if_not_exists' ;
LIST_APPEND     : 'list_append' ;

// Operators
EQ              : '=' ;
PLUS            : '+' ;
MINUS           : '-' ;

// Delimiters
LPAREN          : '(' ;
RPAREN          : ')' ;
LBRACKET        : '[' ;
RBRACKET        : ']' ;
COMMA           : ',' ;
DOT             : '.' ;

// Placeholders
PLACEHOLDER     : ':' [a-zA-Z_] [a-zA-Z0-9_]* ;
NAME_PLACEHOLDER: '#' [a-zA-Z_] [a-zA-Z0-9_]* ;

// Identifiers and numbers
NUMBER          : [0-9]+ ;
IDENTIFIER      : [a-zA-Z_] [a-zA-Z0-9_]* ;

// Skip whitespace
WS              : [ \t\r\n]+ -> skip ;
