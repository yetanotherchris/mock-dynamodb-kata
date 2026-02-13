grammar DynamoDbCondition;

// Parser rules

condition
    : orExpression EOF
    ;

orExpression
    : andExpression (OR andExpression)*
    ;

andExpression
    : notExpression (AND notExpression)*
    ;

notExpression
    : NOT notExpression
    | comparison
    ;

comparison
    : operand comparator operand                                # ComparisonExpr
    | operand BETWEEN operand AND operand                       # BetweenExpr
    | operand IN LPAREN operand (COMMA operand)* RPAREN         # InExpr
    | function                                                  # FunctionExpr
    | LPAREN orExpression RPAREN                                # ParenExpr
    ;

comparator
    : EQ
    | NEQ
    | LT
    | LTE
    | GT
    | GTE
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
    : ATTRIBUTE_EXISTS
    | ATTRIBUTE_NOT_EXISTS
    | ATTRIBUTE_TYPE
    | BEGINS_WITH
    | CONTAINS
    | SIZE
    ;

// Lexer rules

// Keywords (case-insensitive)
AND             : [Aa][Nn][Dd] ;
OR              : [Oo][Rr] ;
NOT             : [Nn][Oo][Tt] ;
BETWEEN         : [Bb][Ee][Tt][Ww][Ee][Ee][Nn] ;
IN              : [Ii][Nn] ;

// Function names
ATTRIBUTE_EXISTS     : 'attribute_exists' ;
ATTRIBUTE_NOT_EXISTS : 'attribute_not_exists' ;
ATTRIBUTE_TYPE       : 'attribute_type' ;
BEGINS_WITH          : 'begins_with' ;
CONTAINS             : 'contains' ;
SIZE                 : 'size' ;

// Operators
EQ              : '=' ;
NEQ             : '<>' ;
LTE             : '<=' ;
GTE             : '>=' ;
LT              : '<' ;
GT              : '>' ;

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
