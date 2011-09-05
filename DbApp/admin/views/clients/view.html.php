<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewClients extends JView {

	function display($tpl = null) {
		// Get data from the model
		$items = $this->get('Items');
		$pagination = $this->get('Pagination');

		// Check for errors.
		if (count($errors = $this->get('Errors'))) 		{
			JError::raiseError(500, implode(' oops . . <br />', $errors));
			return false;
		}
		// Assign data to the view
		$this->items = $items;
		$this->pagination = $pagination;
		// Set the toolbar
		$this->addToolBar();
		// Display the template
		parent::display($tpl);
		// Set the document
		$this->setDocument();
	}

	protected function addToolBar() {
		$canDo = DbAppHelper::getActions();
		JToolBarHelper::title(JText::_('DbApp Client Manager'), 'Client');
		if ($canDo->get('core.create')) {
			JToolBarHelper::addNew('client.add', 'JTOOLBAR_NEW');
		}
		if ($canDo->get('core.edit')) {
			JToolBarHelper::editList('client.edit', 'JTOOLBAR_EDIT');
		}
		if ($canDo->get('core.delete')) {
			JToolBarHelper::deleteList('', 'clients.delete', 'JTOOLBAR_DELETE');
		}
		if ($canDo->get('core.admin')) {
			JToolBarHelper::divider();
			JToolBarHelper::preferences('com_dbapp');
		}
	}

	protected function setDocument() {
		$document = JFactory::getDocument();
		$document->setTitle(JText::_('DbApp Administration'));
	}

}
