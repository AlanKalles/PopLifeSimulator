[Link to the core loop](https://www.figma.com/board/6lLqpgz2ite9aZzYnDpyk6/Poplife?node-id=0-1&p=f&t=Epa38thvW8EqsBhV-0)  
[Link to Spreadsheet](https://docs.google.com/spreadsheets/d/1_qCvlhZ5sylgz-1XzBbcHplGseE1k1LZm2bWC_EidHc/edit?gid=786893397#gid=786893397)  
[Link to Economy System Doc and Notes](https://docs.google.com/document/d/1dZSJepLe3VGBn5FKo19TdAB_2Inq8hVBOnMbwepE-Tw/edit?tab=t.0)  
**Concept Definitions**

**1	Resource**  
**1.1	Game time**: In-game time that flows automatically. Game time pauses when player activates any UI Panel. Actions in the game consume time to finish, including construction, customer actions, etc.

**1.2	Money**: The main currency in the game. At the start of Daily Settlement, if money \< 0, the player will lose and need to restart from the latest checkpoint, usually the day before. Player earns money from operating stores, completing requests, etc. Player uses money to construct 

**1.3	Fame**: A consumable resource. Earned by completing requests (significantly), and in daily settlement (subtly, at the cashier), and in daily achievement. Used to upgrade product category level, buy blueprint, hold promotion event, etc. Harder to gain in the later stages.

**2	Player Goal**

2.1	**Store Star Upgrade**: \[unfinished\] The store can be upgraded from 0 stars to 5 stars.   
2.1.1	**Requirement**: Each upgrade requires certain requirements, and the player can upgrade in an info panel when the requirements are all met. The requirements are usually quotas to meet, not resources to be cost. For example, total shelves built, total customer amount, total shelf level, total customer level, highest historical daily income, etc.  
2.1.2	**Reward**: Each success upgrade rewards the player with a large amount of money and fame, and a special and significant shelf or facility.

2.2	**Customer Request**: Customer will come to the store to make a request, and the player can click to collect them. For new customers with level 0, the request is an easy dialogue choice and the rewards is always money, fame, or customer EXP. For regular customers, the request is always to build or upgrade a specific shelf. The rewards will have new blueprints. A request can have multiple rewards.

2.3	**Main Mission** (VIP Story Mission)  
	VIP Story Mission is the main storyline and main missions in the game. It’s a customer request held by VIP customers, a kind of expanded customer service. The requirements of VIP Missions are not explicit, and players need to solve it as a puzzle through narrative context. There will only exist one active VIP Story Mission at the same time.  
	2.3.1	**Story**:	Each VIP Story Mission has an introductory story that serves as the context of the mission’s requirements. The story is shown in the form of dialogue between VIP customer and player. (See 7.7)

2.4	**Daily Settlement**:  
	Daily settlement count and visualize the data changed during the day, including money and fame earned, served customers, completed and uncompleted achievements etc. (See 3.4.7.2)

2.5	**Daily achievement**

- Daily achievements are tiny, random goals for the player to accomplish. There will always exist three unfinished achievements, and if any of them are achieved, new achievement requirements will be assigned randomly from the database. Status of Daily achievement is displayed during the daily settlement. (See 3.4.7.2)


  
**3	Store**

3.1	**Map**: Multiple unlockable areas in units of Floors. Floors are stacked and connected in an aesthetically irregular manner.  
	3.1.1	**Floor**: Each floor needs a cashier facility. Placement on the floor is grid-based. Each floor is 2-grid height. Each column can only place one shelf. Decoration and Floor connections (stairs and elevators) are placed, but don't occupy the grid.  
	3.1.2	**Customer Capacity**:	The limit of the number of customers that can exist in the store at a time.  
3.1.3	**Unlock New Area**:	The Player can spend money to unlock new areas connected to the unlocked areas. Unlocking New Areas raises the customer capacity.

3.2	\[unfinished\]

3.3	**Shelves**: Placed on the floor, where customers buy products.  
	Shelf attributes:  
3.3.1	**Price**: The customer pays the player this money per product.  
3.3.2	**Maintenance Fee**: Money that’s reduced at daily settlement.   
3.3.3	**Stock**: The Maximum amount of products that can be bought in one day. If stock \= 0, the shelf is sold out and shut down for the rest of the day.  
3.3.4	**Attractiveness:** The more attractive the shelf is, the more possible for a customer to come and buy its product. Shelf attractiveness together is the store’s total attractiveness.  
3.3.5	**Category**: Customer will have an interest level in each category. The customer will visit the shelf from the highest interest level to the lowest. A category can be upgraded from Lv 1 to Lv 5 with a lot of fame and money, and higher levels will need an expensive maintenance fee. Category-level upgrade significantly boosts the attractiveness of all shelves in the category. Category includes: Lingere, Condom, Vibrator, Fleshlights, Lubricant  
3.3.6	**Build Fee**: A one-time money cost when placing the building.  
3.3.7	**Embarrassment Value (EV)**: Add the embarrassment value to the surrounding grid. Formula for each grid: 

- Positive EV: result \+= min(0, EV \- Distance)  
  - Negative EV: result \+= max(0, EV \- Distance)

**3.4**	**Facilities**: Boost floor attributes or have special use. (A short list with varied effects.)   
3.4.1	Facilities attributes:

- **Effect**  
- **Build Fee**  
- **Maintenance Fee**

3.4.2	Facilities examples:

- **Cashier**: Each floor needs at least one cashier. Starting from the second unlocked floor, each cashier needs at least one employee.  
- **Air Conditioner**: Reduce embarrassment Level for all customers on the same floor.  
- **ATM**: Customer with a high interest, low embarrassment, and low money bag will use the ATM to restore their money bag.  
- (...)

3.4.4	**Environmental Embarrassment Value (EEB)**: Each grid has an EEB, affected by the surrounding environment (shelves, facilities, decorations). 

- **Store Embarrassment Heatmap**: A heat map that shows the embarrassment value of each grid.

3.4.5	**Blueprint**: Shelves and Facilities need blueprints to be built. Blueprint is obtained from completing requests or purchased in the blueprint shop with fame.

3.4.6	**Construction Mode**: Player can place, destroy, and move shelves and facilities. Item placement is grid-based.  
1\.	**Place**: Costs a build fee and consumes one blueprint. Construction time: 15 seconds.  
2\.	**Move**: Cost movement fee (small amount). Construction time: 5 seconds  
3\.	**Destroy**: Return one blueprint. No money cost or returned. Instant construction.

3.4.7	**Day Loop**: A Game time day costs 30 seconds in real time. A day counter UI in the corner counts the date in the format: Day xx.  
1\.	**Store Hours**: 12:00 to 23:00. Time flows at normal speed. Customers AI will move and buy products in the store during store hours. The player can activate any UI. The store hours can be upgraded at a mid-stage of gameplay.  
2\.	**Daily Settlement**: UI panel pop-up with information about the last store hours, at 10 pm. During daily settlement, the time flows quickly to 11 am, the next day, then pauses

- Daily Total Sale (all sales)  
  - Daily Total Expenses (all maintenance fees)  
    - Daily Income (sales \- expenses)  
    - Daily Total Customer  
    - **Fame Earned**: A reward to the player, calculated by the daily total income and the daily total customers.  
    - **Daily achievement** (See 2.5)  
    - Competition / News / …

3.4.8	**Store info panel**: A UI panel that keeps track of all store information.

- **Store level**: A store level challenge from 0 stars to 5 stars. Player needs to fulfill specific requirements to upgrade the store level. Upgrading the store level will unlock blueprint and customer, reward money and fame, etc.  
- **Total store count**  
- **Total attractiveness count**  
- **Total income count**

3.4.9	**Promotion Event**:  
	

**4\.	Customers**  
4.1	Customer Attributes:  
4.1.1	**Interest Levels (IL)**: Customer has different interest levels by category. This attribute will not change.  
4.1.2	**Trust Point (TP)**: Trust points are slightly accumulated through successful buy, and through customer upgrades. Trust points will affect the Money bag size. Start with 0\.  
4.1.3	**Embarrassment Point (EP)**: Each customer instance will have its own embarrassment level, separately. Start from 0 when the customer instance enters the store. It’s increased or decreased by the embarrassment level on the grid they stepped on each second. When the Embarrassment level is full, the customer will stop the buying action and run away embarrassedly.  
4.1.4	**Money bag (MB)**: When customers use up all the money, they will leave.  
4.1.5	**EXP**: When EXP is full, upgrade the customer and empty EXP. EXP is gained from completing requests and customers making purchases.

4.2	**Customer Upgrade**: Customers are automatically upgraded to the next level when their request is completed. Upgrade grants customers some Interest Levels (differ between customers). Upgraded customers visit more frequently and buy more products.  
4.2.1	Customer Levels**:**

| Customer Type |  | Buy | Total IL Reward |  |
| :---- | :---- | :---- | :---- | :---- |
| **New Customer** |  | 1 \- 2 | 1 \- 2 |  |
| **Regular** |  | 1 \- 5 | 2 \- 3 |  |
| **Member** |  | 1 \- 7 | 2 \- 4 |  |
| **Advocate** |  | 1 \- 10 | 3 \- 5 |  |

	\[unfinished 目前没有限制visit，同时只能有一个同样customer；购买量计算 IL x Trust Level\]  
4.3	**Customer AI**:   
4.3.1	**Embarrassment Point**: A customer attribute, starting with 0\. Each second, add the environmental embarrassment value of the customer’s standing grid to their embarrassment point. The customer will leave when the Embarrassment point is full. Embarrassment Point cannot be below 0\.  
4.3.2	**Spawn**: Each customer can only visit once per day. Randomly pick customers to spawn until the customer capacity limit is hit.  
4.3.3	**Buy**: The range of amount of products the customer will try to purchase at one shelf.  
4.3.4	**Buy Amount:** The actual amount of product the customer will try to purchase at one shelf.  
4.3.5	**Buy List**: A list of shelves that the customer will probably visit.   
4.3.6	**Inventory**: Each customer has an inventory. The customer will hold the products in inventory in hand. If the customer bought it, remove the inventory visual and add a shopping bag visual.  
4.3.7	**Time**: Time to wait for each action. Other customers trying to interact with the occupied object need to line up and wait till they’re finished.  
4.3.8	**Target**: The Customer will always move towards the target.  
4.3.9	*Customer AI Logic**:***

1. **Customers randomly spawn at the entrance.** A customer spawns no less than min-visit and no more than max-visit times in a day, randomly.  
2. **Set the target to the most attractive shelf** for this customer.   
   The selection logic:   
1. Create a list of shelves which’s in the category (/categories) with the highest customer interest point.  
2. Randomly select a target shelf from the list. The weight of random selection is the attractiveness of the shelf.  
3. Handle exceptions: If the way to the shelf doesn’t exist, remove the shelf from the list. Move to 1\. (If this happens 3 times straight, the customer is annoyed and leaves.)  
4. Move to the target shelf. Wait in line.  
5. **Calculate the buy amount**. Randomly select between min-buy and max-buy.  
6. Handle exceptions: If {(buy amount) \* (shelf price) \> money bag}, then {buy amount \= money bag shelf price}. If buy amount \< 0, Customer leaves. \[unfinished\]  
7. Shelf stock \-= buy amount.   
8. **Make Payment**: Customers add the buy amount of the product to the inventory. Move to the closest register. Line up, make payment.  
9. Payment \= (buy amount) \* (shelf price). Customer's money bag \-= payment. Player money \+= payment.  
10. Empty player inventory. Remove the bought shelf from the customer's buying list.  
11. **Move to step 2**, loop.

4.3.10	*Customer AI Notes*:  
1\.	

4.4	**Customer Actions**:   
Walk speed \= \[unfinished, need tests\]. Customer speed is not changeable by the player.

| Action | Time cost (seconds) | Notes |
| :---- | :---- | :---- |
| **Move** | Distance/speed |  |
| **Wait** | (line position) \* (checkout/ buy time) |  |
| **Buy** | 1 \+ (buy amount \* 0.1) second |  |
| **Checkout** | 1 \+ (buy amount \* 0.1) second |  |

4.5	**Customer Traits**:  
	4.5.1 **Base properties**

| Lingere | Condom | Vibrator | Fleshlights | Lubricant | BDSM | Money bag |
| ----- | ----- | :---- | ----- | ----- | ----- | ----- |
| 2 | 2 | 2 | 2 | 2 | 2 | 50 |

	4.5.2 **Trait List**[PoplifeSpreadSheet](https://docs.google.com/spreadsheets/d/1_qCvlhZ5sylgz-1XzBbcHplGseE1k1LZm2bWC_EidHc/edit?gid=594297129#gid=594297129)

**5\.	Storytelling**  
5.1	**Story Summary**	  
\[unfinished\]  
5.2	**Background Setting**	  
\[unfinished\]  
5.3	**Storytelling Tools**  
	Story in the game will be narrated mainly through VIP Story Mission. Flavor text will help with world-building in Customer Request, Shelf info panel, Customer info panel, etc.  
We are using Dialogue Tree in [Node Canvas](https://nodecanvas.paradoxnotion.com/documentation/) to implement dialogues into gameplay.   
	5.3.1	**Tutorial Dialogue**:  
	[Link to Tutorial Dialogue](https://docs.google.com/spreadsheets/d/1_qCvlhZ5sylgz-1XzBbcHplGseE1k1LZm2bWC_EidHc/edit?gid=1982605657#gid=1982605657).  
	\[unfinished\]  
5.3.2	**VIP Mission Dialogue**:  
[Link to VIP Mission Dialogue](https://docs.google.com/spreadsheets/d/1_qCvlhZ5sylgz-1XzBbcHplGseE1k1LZm2bWC_EidHc/edit?gid=1667879510#gid=1667879510).  
\[unfinished\]

**6\.	Art**	\[unfinished\]  
6.1	Artstyle/reference  
6.2	Character  
6.3	Store  
6.4 	Scene  
6.5	Technical Art  
…

**7\.	UI**	\[unfinished\]  
7.1	General UI  
7.2	Runtime Indicator UI:  
	**Pop Bubble**: Overhead, indicating customer action, including getting products from shelves, purchasing, thoughts, and emotions, etc.   
	**Broadcast**: A one-line message about each customer's Purchase, rolling horizontally. Example format of each message: \[customer name\] bought \[amount\] of \[product\], \+ \[sales money\].   
	**Messages**: A list of messages about important notices. Text will be in three colors: Gold, White, and Red.  
	\-	**Gold**: Notification of the occurrence of something exciting  
		\-	  
	\-	**White**: Notification of the occurrence of something normal  
	\-	**Red**: Warning messages of the occurrence of something negative.  
		\-	Customer leaves because no interesting shelves available  
		\-	…  
Unlock/ Upgrade/ important, etc.  
7.3	Statistic Panel:  
	**Daily Settlement Panel**:  
7.4	**Construction Panel**:  
7.5	**Information Panel:**  
	7.5.1	Customer  
	7.5.2	Shelf  
	7.5.3	…  
7.6	**Request**:  
	7.6.1	Request List  
	7.6.2	Request Detail  
7.7	**VIP Mission**:  
	

**8\.	Audio**	\[unfinished\]  
8.1	Music  
8.2	SFX  
8.3	Audio Implementation  
…